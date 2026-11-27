using System.Collections.Immutable;
using System.Numerics;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Instructions;

namespace WasmSharp.Modules;

/// <summary>
/// 1関数の型検査と線形実行コードの生成を行う
/// </summary>
internal static class FunctionValidator
{
    /// <summary>
    /// 定義関数の参照と値スタックの型を検証し、対応する線形実行コードを作成する
    /// </summary>
    /// <remarks>入力の型配列とmoduleの検証成功状態・実行コードは変更しない</remarks>
    /// <param name="module">importと定義関数の型参照、global初期化式、リソース宣言、要素・データセグメントの検証を終えたmodule</param>
    /// <param name="definitionIndex">importを含まない定義順の関数index</param>
    /// <param name="moduleFunctionIndex">型の参照と診断に使用する、importを含むmodule全体の関数index</param>
    /// <param name="globalTypes">importを先頭とするglobal index順の解決済みの型</param>
    /// <param name="references">当該module検証で共有する参照情報</param>
    /// <returns>実行命令と確定済み分岐情報、追加localsとoperandの最大要素数を持つ、この定義関数の実行コード</returns>
    /// <exception cref="WasmValidateException">命令の参照先、globalの可変性、入力型または結果の型・個数・順序が不正な場合</exception>
    /// <exception cref="InvalidOperationException">デコード済み命令の記述子や検証規則が実装内で一致しない場合</exception>
    internal static FunctionCode Validate(
        WasmModule module,
        int definitionIndex,
        uint moduleFunctionIndex,
        WasmGlobalType[] globalTypes,
        ReferenceValidationContext references
    )
    {
        var function = module.Functions[definitionIndex];
        var type = references.FunctionTypes[(int)moduleFunctionIndex];
        var instructions = ImmutableArray.CreateBuilder<Instruction>(function.Instructions.Length);
        var branchTargets = ImmutableArray.CreateBuilder<BranchTarget>();
        ImmutableArray<WasmValueType>.Builder? castTargets = null;
        List<OperandType> stack = [];
        HashSet<uint> initializedLocals = [];
        var initializationOrder = new Stack<uint>();
        List<ControlFrame> frames = [new(ValidationRule.FunctionEnd, [], type.ResultTypes, 0, 0, 0)];
        uint previousLocalEnd = 0;
        foreach (var local in function.Locals)
        {
            if (local.EndIndex > previousLocalEnd)
            {
                local.Type.Validate();
                previousLocalEnd = local.EndIndex;
            }
        }
        var maxOperandStack = 0;
        var instructionSet = InstructionSet.ForVersion(module.SpecVersion);
        foreach (var instruction in function.Instructions)
        {
            if (!instructionSet.TryGetValue(instruction.Opcode, out var descriptor))
            {
                throw new InvalidOperationException("デコード済み命令の情報がありません。");
            }

            var emitInstruction = true;
            var outputIndex = instruction.Index;
            var outputSecondaryIndex = instruction.SecondaryIndex;
            var outputTargetCount = 0;

            switch (descriptor.Validation)
            {
                case ValidationRule.None:
                    break;

                case ValidationRule.Block:
                case ValidationRule.Loop:
                case ValidationRule.If:
                    var (parameters, results) = ResolveBlockType(instruction.BlockType, instruction.ByteOffset);
                    if (descriptor.Validation == ValidationRule.If)
                    {
                        Pop(WasmValueKind.I32, instruction.ByteOffset);
                    }
                    PopTypes(parameters.AsSpan(), instruction.ByteOffset);
                    var enteredFrame = new ControlFrame(
                        descriptor.Validation,
                        parameters,
                        results,
                        stack.Count,
                        instructions.Count,
                        initializationOrder.Count
                    );
                    frames.Add(enteredFrame);
                    PushTypes(parameters.AsSpan());
                    if (descriptor.Validation == ValidationRule.If)
                    {
                        enteredFrame.IfFalseTarget = AddTarget(enteredFrame, parameters.Length);
                        outputIndex = (uint)enteredFrame.IfFalseTarget;
                    }
                    else
                    {
                        emitInstruction = false;
                    }
                    break;

                case ValidationRule.Else:
                    var ifFrame = frames[^1];
                    CheckEnd(ifFrame, instruction.ByteOffset);
                    ifFrame.HasElse = true;
                    ifFrame.Unreachable = false;
                    PushTypes(ifFrame.Parameters.AsSpan());
                    outputIndex = (uint)AddTarget(ifFrame, ifFrame.Results.Length);
                    ifFrame.ForwardTargets.Add((int)outputIndex);
                    // elseの跳躍命令の次が、条件0で進むelse本体の入口
                    ifFrame.ElsePc = instructions.Count + 1;
                    break;

                case ValidationRule.Constant:
                    stack.Add(
                        descriptor.StackEffect switch
                        {
                            StackEffectKind.PushI32 => WasmValueKind.I32,
                            StackEffectKind.PushI64 => WasmValueKind.I64,
                            StackEffectKind.PushF32 => WasmValueKind.F32,
                            StackEffectKind.PushF64 => WasmValueKind.F64,
                            StackEffectKind.PushV128 => WasmValueKind.V128,
                            StackEffectKind.PushReference => instruction.ReferenceType.Validate(),
                            _ => throw new InvalidOperationException("定数命令のスタック効果が不正です。"),
                        }
                    );
                    break;

                case ValidationRule.RefFunc:
                    if (instruction.Index >= references.FunctionCount)
                    {
                        throw CreateUnknownIndex(
                            "function",
                            instruction.Index,
                            "参照する関数が存在しません。",
                            instruction.ByteOffset
                        );
                    }
                    if (!references.DeclaredFunctions.Contains(instruction.Index))
                    {
                        throw ValidationDiagnostics.ValidationFailure(
                            "undeclared function reference",
                            "参照する関数が宣言されていません。",
                            new(WasmProcessingStage.Validate, instruction.ByteOffset, moduleFunctionIndex, 10)
                        );
                    }
                    stack.Add(references.FunctionTypes[(int)instruction.Index].GetRefFuncResultType(module.SpecVersion));
                    break;

                case ValidationRule.RefIsNull:
                    PopReference(instruction.ByteOffset, "null判定");
                    stack.Add(WasmValueKind.I32);
                    break;

                case ValidationRule.RefEq:
                    Pop(WasmValueKind.EqRef, instruction.ByteOffset);
                    Pop(WasmValueKind.EqRef, instruction.ByteOffset);
                    stack.Add(WasmValueKind.I32);
                    break;

                case ValidationRule.RefTest:
                case ValidationRule.RefCast:
                    var targetType = instruction.ReferenceType.Validate();
                    Pop(targetType.Kind.GetTopKind(), instruction.ByteOffset);
                    stack.Add(descriptor.Validation == ValidationRule.RefTest ? WasmValueKind.I32 : targetType);
                    outputSecondaryIndex = AddCastTarget(targetType);
                    break;

                case ValidationRule.StructNew:
                case ValidationRule.StructNewDefault:
                case ValidationRule.StructGet:
                case ValidationRule.StructGetPacked:
                case ValidationRule.StructSet:
                    ValidateStructInstruction(instruction, descriptor.Validation);
                    break;

                case ValidationRule.I31Get:
                    Pop(WasmValueKind.I31Ref, instruction.ByteOffset);
                    stack.Add(WasmValueKind.I32);
                    break;

                case ValidationRule.RefAsNonNull:
                    stack.Add(PopReference(instruction.ByteOffset).AsNonNullable());
                    break;

                case ValidationRule.Numeric:
                    switch (descriptor.Immediate)
                    {
                        case ImmediateKind.LaneIndex:
                            ValidateLaneIndex(instruction.LaneIndex, descriptor.LaneCount, instruction.ByteOffset);
                            break;
                        case ImmediateKind.Shuffle:
                            foreach (var laneIndex in instruction.ShuffleLanes)
                            {
                                ValidateLaneIndex(laneIndex, 32, instruction.ByteOffset);
                            }
                            break;
                    }
                    var numericSignature = descriptor.NumericSignature
                        ?? throw new InvalidOperationException("数値命令の型情報がありません。");
                    for (var index = numericSignature.InputTypes.Length - 1; index >= 0; index--)
                    {
                        Pop(numericSignature.InputTypes[index], instruction.ByteOffset);
                    }
                    stack.Add(numericSignature.ResultType);
                    break;

                case ValidationRule.MemoryLoad:
                case ValidationRule.MemoryStore:
                case ValidationRule.MemoryLoadLane:
                case ValidationRule.MemoryStoreLane:
                    var (memorySignature, addressType) = ValidateMemoryAccess(instruction, descriptor);
                    if (descriptor.Validation is ValidationRule.MemoryLoadLane or ValidationRule.MemoryStoreLane)
                    {
                        ValidateLaneIndex(instruction.LaneIndex, descriptor.LaneCount, instruction.ByteOffset);
                    }
                    if (
                        descriptor.Validation
                        is ValidationRule.MemoryStore
                            or ValidationRule.MemoryLoadLane
                            or ValidationRule.MemoryStoreLane
                    )
                    {
                        Pop(memorySignature.ValueType, instruction.ByteOffset);
                    }
                    Pop(addressType, instruction.ByteOffset);
                    if (descriptor.Validation is ValidationRule.MemoryLoad or ValidationRule.MemoryLoadLane)
                    {
                        stack.Add(memorySignature.ValueType);
                    }
                    break;

                case ValidationRule.MemorySize:
                case ValidationRule.MemoryGrow:
                    var pageCountType = GetMemoryAddressType(instruction.Index, instruction.ByteOffset);
                    if (descriptor.Validation == ValidationRule.MemoryGrow)
                    {
                        Pop(pageCountType, instruction.ByteOffset);
                    }
                    stack.Add(pageCountType);
                    break;

                case ValidationRule.MemoryCopy:
                    var destinationAddressType = GetMemoryAddressType(instruction.Index, instruction.ByteOffset);
                    var sourceAddressType = GetMemoryAddressType(instruction.SecondaryIndex, instruction.ByteOffset);
                    PopCopyOperands(destinationAddressType, sourceAddressType, instruction.ByteOffset);
                    break;

                case ValidationRule.MemoryFill:
                    var fillAddressType = GetMemoryAddressType(instruction.Index, instruction.ByteOffset);
                    Pop(fillAddressType, instruction.ByteOffset);
                    Pop(WasmValueKind.I32, instruction.ByteOffset);
                    Pop(fillAddressType, instruction.ByteOffset);
                    break;

                case ValidationRule.MemoryInit:
                    var initializationAddressType = GetMemoryAddressType(instruction.SecondaryIndex, instruction.ByteOffset);
                    RequireDataSegment(instruction.Index, instruction.ByteOffset);
                    Pop(WasmValueKind.I32, instruction.ByteOffset);
                    Pop(WasmValueKind.I32, instruction.ByteOffset);
                    Pop(initializationAddressType, instruction.ByteOffset);
                    break;

                case ValidationRule.DataDrop:
                    RequireDataSegment(instruction.Index, instruction.ByteOffset);
                    break;

                case ValidationRule.TableGet:
                case ValidationRule.TableSet:
                case ValidationRule.TableGrow:
                case ValidationRule.TableSize:
                case ValidationRule.TableFill:
                    var tableType = GetTableType(instruction.Index, instruction.ByteOffset);
                    switch (descriptor.Validation)
                    {
                        case ValidationRule.TableGet:
                            Pop(tableType.AddressType, instruction.ByteOffset);
                            stack.Add(tableType.ElementValueType);
                            break;
                        case ValidationRule.TableSet:
                            Pop(tableType.ElementValueType, instruction.ByteOffset);
                            Pop(tableType.AddressType, instruction.ByteOffset);
                            break;
                        case ValidationRule.TableGrow:
                            Pop(tableType.AddressType, instruction.ByteOffset);
                            Pop(tableType.ElementValueType, instruction.ByteOffset);
                            stack.Add(tableType.AddressType);
                            break;
                        case ValidationRule.TableSize:
                            stack.Add(tableType.AddressType);
                            break;
                        case ValidationRule.TableFill:
                            Pop(tableType.AddressType, instruction.ByteOffset);
                            Pop(tableType.ElementValueType, instruction.ByteOffset);
                            Pop(tableType.AddressType, instruction.ByteOffset);
                            break;
                    }
                    break;

                case ValidationRule.TableCopy:
                    var destinationType = GetTableType(instruction.Index, instruction.ByteOffset);
                    var sourceType = GetTableType(instruction.SecondaryIndex, instruction.ByteOffset);
                    if (!destinationType.ElementValueType.Accepts(sourceType.ElementValueType))
                    {
                        throw CreateException(
                            "コピー元とコピー先のテーブルの参照型が一致しません。",
                            instruction.ByteOffset
                        );
                    }
                    PopCopyOperands(destinationType.AddressType, sourceType.AddressType, instruction.ByteOffset);
                    break;

                case ValidationRule.TableInit:
                    // バイナリの即値順と分け、テーブル、要素、型、operandの順に検査する。
                    var initializationTableType = GetTableType(instruction.SecondaryIndex, instruction.ByteOffset);
                    var elementType = GetElementType(instruction.Index, instruction.ByteOffset);
                    if (!initializationTableType.ElementValueType.Accepts(elementType))
                    {
                        throw CreateException(
                            "要素セグメントと対象テーブルの参照型が一致しません。",
                            instruction.ByteOffset
                        );
                    }
                    Pop(WasmValueKind.I32, instruction.ByteOffset);
                    Pop(WasmValueKind.I32, instruction.ByteOffset);
                    Pop(initializationTableType.AddressType, instruction.ByteOffset);
                    break;

                case ValidationRule.ElementDrop:
                    GetElementType(instruction.Index, instruction.ByteOffset);
                    break;

                case ValidationRule.FunctionEnd:
                    var endedFrame = frames[^1];
                    CheckEnd(endedFrame, instruction.ByteOffset);
                    if (endedFrame.Kind == ValidationRule.If)
                    {
                        if (!endedFrame.HasElse && !endedFrame.CanOmitElse())
                        {
                            throw CreateException(
                                "elseを省略したifの引数型と結果型が一致しません。",
                                instruction.ByteOffset
                            );
                        }
                        var currentPc = instructions.Count;
                        PatchTarget(endedFrame.IfFalseTarget, endedFrame.HasElse ? endedFrame.ElsePc : currentPc);
                    }
                    // 関数frameの前方参照も、配列末尾ではなく末尾ReturnのPcへ接続する。
                    foreach (var targetIndex in endedFrame.ForwardTargets)
                    {
                        PatchTarget(targetIndex, instructions.Count);
                    }
                    frames.RemoveAt(frames.Count - 1);
                    if (endedFrame.Kind != ValidationRule.FunctionEnd)
                    {
                        PushTypes(endedFrame.Results.AsSpan());
                        emitInstruction = false;
                    }
                    break;

                case ValidationRule.Call:
                    if (instruction.Index >= references.FunctionCount)
                    {
                        throw CreateUnknownIndex(
                            "function",
                            instruction.Index,
                            "参照する関数が存在しません。",
                            instruction.ByteOffset
                        );
                    }
                    ApplyCallType(references.FunctionTypes[(int)instruction.Index], instruction.ByteOffset);
                    break;

                case ValidationRule.CallIndirect:
                    // バイナリの即値順と分け、テーブル、型、funcref制約、operandの順に検査する。
                    var indirectTableType = GetTableType(instruction.SecondaryIndex, instruction.ByteOffset);
                    var indirectCalleeType = GetTypeSignature(instruction.Index, instruction.ByteOffset);
                    if (indirectTableType.ElementType != WasmValueKind.FuncRef)
                    {
                        throw CreateException("間接呼び出し先のテーブルにはfuncrefが必要です。", instruction.ByteOffset);
                    }
                    Pop(indirectTableType.AddressType, instruction.ByteOffset);
                    ApplyCallType(indirectCalleeType, instruction.ByteOffset);
                    break;

                case ValidationRule.CallRef:
                    var referencedType = GetTypeSignature(instruction.Index, instruction.ByteOffset);
                    Pop(WasmValueType.FunctionReference(referencedType, isNullable: true), instruction.ByteOffset);
                    ApplyCallType(referencedType, instruction.ByteOffset);
                    break;

                case ValidationRule.Return:
                    PopTypes(type.ResultTypes.AsSpan(), instruction.ByteOffset);
                    MarkUnreachable();
                    break;

                case ValidationRule.Branch:
                case ValidationRule.BranchIf:
                    // ラベル参照の不成立は、条件や分岐値の不成立より先に通知する。
                    var labelFrame = GetLabel(instruction.Index, instruction.ByteOffset);
                    if (descriptor.Validation == ValidationRule.BranchIf)
                    {
                        Pop(WasmValueKind.I32, instruction.ByteOffset);
                    }
                    PopTypes(labelFrame.LabelTypes.AsSpan(), instruction.ByteOffset);
                    outputIndex = (uint)AddLabelTarget(labelFrame);
                    if (descriptor.Validation == ValidationRule.BranchIf)
                    {
                        PushTypes(labelFrame.LabelTypes.AsSpan());
                    }
                    else
                    {
                        MarkUnreachable();
                    }
                    break;

                case ValidationRule.BranchOnNull:
                    var nullLabel = GetLabel(instruction.Index, instruction.ByteOffset);
                    var testedReference = PopReference(instruction.ByteOffset);
                    PopTypes(nullLabel.LabelTypes.AsSpan(), instruction.ByteOffset);
                    PushTypes(nullLabel.LabelTypes.AsSpan());
                    stack.Add(testedReference.AsNonNullable());
                    outputIndex = (uint)AddLabelTarget(nullLabel);
                    break;

                case ValidationRule.BranchOnNonNull:
                    var nonNullLabel = GetLabel(instruction.Index, instruction.ByteOffset);
                    var labelTypes = nonNullLabel.LabelTypes.AsSpan();
                    if (labelTypes.IsEmpty || !labelTypes[^1].IsReference)
                    {
                        throw CreateException("分岐先の最後の値には参照型が必要です。", instruction.ByteOffset);
                    }
                    CheckType(PopReference(instruction.ByteOffset).AsNonNullable(), labelTypes[^1], instruction.ByteOffset);
                    var precedingTypes = labelTypes[..^1];
                    PopTypes(precedingTypes, instruction.ByteOffset);
                    PushTypes(precedingTypes);
                    outputIndex = (uint)AddLabelTarget(nonNullLabel);
                    break;

                case ValidationRule.BranchOnCast:
                case ValidationRule.BranchOnCastFail:
                    (outputIndex, outputSecondaryIndex) = ValidateCastBranch(
                        instruction,
                        descriptor.Validation == ValidationRule.BranchOnCast
                    );
                    break;

                case ValidationRule.BranchTable:
                    var defaultLabel = GetLabel(instruction.Labels[^1], instruction.ByteOffset);
                    var branchTypes = new OperandType[defaultLabel.LabelTypes.Length];
                    for (var index = 0; index < branchTypes.Length; index++)
                    {
                        // selectorを除く同じ列を参照する。不足は最後のPopで検査する。
                        branchTypes[index] = Peek(branchTypes.Length - index);
                    }
                    CheckLabelTypes(branchTypes, defaultLabel.LabelTypes, instruction.ByteOffset);
                    outputIndex = (uint)branchTargets.Count;
                    outputTargetCount = instruction.Labels.Length - 1;
                    for (var index = 0; index < outputTargetCount; index++)
                    {
                        // 候補は入力順に解決し、型適合を確認してから次のラベルへ進む。
                        var candidateLabel = GetLabel(instruction.Labels[index], instruction.ByteOffset);
                        CheckLabelTypes(branchTypes, candidateLabel.LabelTypes, instruction.ByteOffset);
                        AddLabelTarget(candidateLabel);
                    }
                    AddLabelTarget(defaultLabel);
                    Pop(WasmValueKind.I32, instruction.ByteOffset);
                    for (var index = branchTypes.Length - 1; index >= 0; index--)
                    {
                        Pop(branchTypes[index].DeclaredType, instruction.ByteOffset);
                    }
                    MarkUnreachable();
                    break;

                case ValidationRule.Unreachable:
                    MarkUnreachable();
                    break;

                case ValidationRule.GlobalGet:
                case ValidationRule.GlobalSet:
                    if (instruction.Index >= (uint)globalTypes.Length)
                    {
                        throw CreateUnknownIndex(
                            "global",
                            instruction.Index,
                            "参照するglobalが存在しません。",
                            instruction.ByteOffset
                        );
                    }
                    var globalType = globalTypes[(int)instruction.Index];
                    if (descriptor.Validation == ValidationRule.GlobalGet)
                    {
                        stack.Add(globalType.ValueType);
                    }
                    else
                    {
                        if (!globalType.IsMutable)
                        {
                            throw ValidationDiagnostics.ValidationFailure(
                                module.SpecVersion == WasmSpecVersion.Core3 ? "immutable global" : "global is immutable",
                                "immutable globalは更新できません。",
                                new(WasmProcessingStage.Validate, instruction.ByteOffset, moduleFunctionIndex, 10)
                            );
                        }
                        Pop(globalType.ValueType, instruction.ByteOffset);
                    }
                    break;

                case ValidationRule.Drop:
                    Pop(null, instruction.ByteOffset);
                    break;

                case ValidationRule.Select:
                case ValidationRule.TypedSelect:
                    ValidateSelect(instruction, descriptor.Validation == ValidationRule.TypedSelect);
                    break;

                case ValidationRule.LocalGet:
                case ValidationRule.LocalSet:
                case ValidationRule.LocalTee:
                    var localType = GetLocalType(instruction.Index, instruction.ByteOffset);
                    var needsInitialization =
                        instruction.Index >= (uint)type.ParameterTypes.Length
                        && localType is { IsReference: true, IsNullable: false };
                    if (
                        descriptor.Validation == ValidationRule.LocalGet
                        && needsInitialization
                        && !initializedLocals.Contains(instruction.Index)
                    )
                    {
                        throw ValidationDiagnostics.ValidationFailure(
                            "uninitialized local",
                            "初期化していないlocalを読み取ることはできません。",
                            new WasmFailureLocation(
                                WasmProcessingStage.Validate,
                                instruction.ByteOffset,
                                moduleFunctionIndex,
                                10
                            )
                        );
                    }
                    if (descriptor.Validation != ValidationRule.LocalGet)
                    {
                        Pop(localType, instruction.ByteOffset);
                        if (needsInitialization && initializedLocals.Add(instruction.Index))
                        {
                            initializationOrder.Push(instruction.Index);
                        }
                    }
                    if (descriptor.Validation != ValidationRule.LocalSet)
                    {
                        stack.Add(localType);
                    }
                    break;

                case ValidationRule.ConstantReference:
                    var referenceOperation = descriptor.ConstantReferenceOperation!;
                    var input = Pop(referenceOperation.InputType, instruction.ByteOffset);
                    stack.Add(referenceOperation.GetResultType(input.DeclaredType));
                    break;

                default:
                    throw new InvalidOperationException("デコード済み命令の検証規則が不正です。");
            }

            maxOperandStack = Math.Max(maxOperandStack, stack.Count);
            if (emitInstruction)
            {
                instructions.Add(
                    new Instruction(
                        descriptor.ExecutionOpcode!.Value,
                        instruction.Immediate,
                        instruction.ByteOffset,
                        outputIndex,
                        outputTargetCount,
                        instruction.MemoryArgument,
                        descriptor.MemoryAccessSignature.GetValueOrDefault(),
                        outputSecondaryIndex,
                        instruction.ShuffleLanes,
                        instruction.LaneIndex
                    )
                );
            }
        }

        return new FunctionCode(
            instructions.ToImmutable(),
            function.Locals.AsSpan(),
            maxOperandStack,
            branchTargets.ToImmutable(),
            castTargets?.ToImmutable() ?? []
        );

        (uint BranchIndex, uint CastIndex) ValidateCastBranch(DecodedInstruction instruction, bool branchOnSuccess)
        {
            var (source, target) = instruction.CastBranchTypes;
            source.Validate();
            target.Validate();
            if (!source.Accepts(target))
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "type mismatch on cast",
                    "キャスト先はキャスト元の部分型である必要があります。",
                    new WasmFailureLocation(WasmProcessingStage.Validate, instruction.ByteOffset, moduleFunctionIndex, 10)
                );
            }

            // nullもキャストに成功する場合、失敗側では元の参照型からnullを除ける。
            var failureType = target.IsNullable ? source.AsNonNullable() : source;
            var branchType = branchOnSuccess ? target : failureType;
            var label = GetLabel(instruction.Index, instruction.ByteOffset);
            var labelTypes = label.LabelTypes.AsSpan();
            if (labelTypes.IsEmpty || !labelTypes[^1].Accepts(branchType))
            {
                throw CreateException("分岐先の最後の型がキャスト分岐で渡す参照に一致しません。", instruction.ByteOffset);
            }

            Pop(source, instruction.ByteOffset);
            var precedingTypes = labelTypes[..^1];
            PopTypes(precedingTypes, instruction.ByteOffset);
            PushTypes(precedingTypes);
            stack.Add(branchOnSuccess ? failureType : target);
            return ((uint)AddLabelTarget(label), AddCastTarget(target));
        }

        uint AddCastTarget(WasmValueType target)
        {
            castTargets ??= ImmutableArray.CreateBuilder<WasmValueType>();
            var index = (uint)castTargets.Count;
            castTargets.Add(target);
            return index;
        }

        void PopCopyOperands(WasmValueKind destinationType, WasmValueKind sourceType, long byteOffset)
        {
            // 転送長には、コピー先とコピー元のうち幅が小さい方のアドレス型を使う。
            var lengthType = destinationType == sourceType ? destinationType : WasmValueKind.I32;
            Pop(lengthType, byteOffset);
            Pop(sourceType, byteOffset);
            Pop(destinationType, byteOffset);
        }

        void RequireDataSegment(uint index, long byteOffset)
        {
            if (index >= (uint)module.DataSegments.Length)
            {
                throw CreateUnknownIndex("data segment", index, "参照するデータセグメントが存在しません。", byteOffset);
            }
        }

        void ValidateStructInstruction(DecodedInstruction instruction, ValidationRule rule)
        {
            var offset = instruction.ByteOffset;
            var type = StructInstructionValidation.RequireType(
                module.Types.AsSpan(),
                instruction.Index,
                offset,
                10,
                moduleFunctionIndex
            );
            if (rule is ValidationRule.StructNew or ValidationRule.StructNewDefault)
            {
                if (rule == ValidationRule.StructNewDefault)
                {
                    StructInstructionValidation.RequireDefaultable(type, offset, 10, moduleFunctionIndex);
                }
                else
                {
                    for (var index = type.Fields.Length - 1; index >= 0; index--)
                    {
                        Pop(type.Fields[index].ValueType, offset);
                    }
                }
                stack.Add(WasmValueType.StructReference(type, isNullable: false));
                return;
            }

            var field = StructInstructionValidation.RequireField(
                type,
                instruction.SecondaryIndex,
                rule,
                offset,
                10,
                moduleFunctionIndex
            );
            var receiver = WasmValueType.StructReference(type, isNullable: true);
            if (rule == ValidationRule.StructSet)
            {
                Pop(field.ValueType, offset);
                Pop(receiver, offset);
            }
            else
            {
                Pop(receiver, offset);
                stack.Add(field.ValueType);
            }
        }

        WasmFunctionType GetTypeSignature(uint index, long byteOffset)
        {
            return ModuleValidator.RequireFunctionType(
                module,
                index,
                byteOffset,
                10,
                moduleFunctionIndex,
                "参照する型が存在しません。"
            );
        }

        void ApplyCallType(WasmFunctionType calleeType, long byteOffset)
        {
            PopTypes(calleeType.ParameterTypes.AsSpan(), byteOffset);
            PushTypes(calleeType.ResultTypes.AsSpan());
        }

        WasmTableType GetTableType(uint index, long byteOffset)
        {
            if (index >= (uint)references.TableTypes.Length)
            {
                throw CreateUnknownIndex("table", index, "参照するテーブルが存在しません。", byteOffset);
            }
            return references.TableTypes[(int)index];
        }

        WasmValueType GetElementType(uint index, long byteOffset)
        {
            if (index >= (uint)references.ElementTypes.Length)
            {
                throw CreateUnknownIndex("elem segment", index, "参照する要素セグメントが存在しません。", byteOffset);
            }
            return references.ElementTypes[(int)index];
        }

        void ValidateLaneIndex(byte laneIndex, byte laneCount, long byteOffset)
        {
            if (laneIndex >= laneCount)
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "invalid lane index",
                    "laneの添字が命令の上限を超えています。",
                    new(WasmProcessingStage.Validate, byteOffset, moduleFunctionIndex, 10)
                );
            }
        }

        WasmValueKind GetMemoryAddressType(uint index, long byteOffset)
        {
            if (index >= references.MemoryCount)
            {
                throw CreateUnknownIndex("memory", index, "参照するメモリが存在しません。", byteOffset);
            }
            return references.MemoryAddressTypes[(int)index];
        }

        (MemoryAccessSignature Signature, WasmValueKind AddressType) ValidateMemoryAccess(
            DecodedInstruction instruction,
            InstructionDescriptor descriptor
        )
        {
            var addressType = GetMemoryAddressType(instruction.Index, instruction.ByteOffset);
            var memorySignature = descriptor.MemoryAccessSignature
                ?? throw new InvalidOperationException("メモリ命令のアクセス情報がありません。");
            if (instruction.MemoryArgument.Alignment > (uint)BitOperations.Log2(memorySignature.ByteWidth))
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "alignment must not be larger than natural",
                    "アラインメントが命令のアクセス幅を超えています。",
                    new(WasmProcessingStage.Validate, instruction.ByteOffset, moduleFunctionIndex, 10)
                );
            }
            if (addressType == WasmValueKind.I32 && instruction.MemoryArgument.Offset > uint.MaxValue)
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "offset out of range",
                    "オフセットが32ビットメモリの上限を超えています。",
                    new WasmFailureLocation(WasmProcessingStage.Validate, instruction.ByteOffset, moduleFunctionIndex, 10)
                );
            }
            return (memorySignature, addressType);
        }

        (ImmutableArray<WasmValueType> Parameters, ImmutableArray<WasmValueType> Results) ResolveBlockType(
            BlockType blockType,
            long byteOffset
        )
        {
            switch (blockType.Kind)
            {
                case BlockTypeKind.Empty:
                    return ([], []);
                case BlockTypeKind.Value:
                    return ([], [blockType.ValueType.Validate()]);
                case BlockTypeKind.TypeIndex:
                    var blockSignature = GetTypeSignature(blockType.TypeIndex, byteOffset);
                    return (blockSignature.ParameterTypes, blockSignature.ResultTypes);
                default:
                    throw new InvalidOperationException("制御の型指定が不正です。");
            }
        }

        void ValidateSelect(DecodedInstruction instruction, bool typed)
        {
            WasmValueType? candidateType;
            if (typed)
            {
                // 型vector長は、候補の型やoperand不足より先に検査する。
                if (instruction.SelectTypes.Length != 1)
                {
                    throw ValidationDiagnostics.ValidationFailure(
                        "invalid result arity",
                        "型注釈付きselectの結果型は1個である必要があります。",
                        new(WasmProcessingStage.Validate, instruction.ByteOffset, moduleFunctionIndex, 10)
                    );
                }
                candidateType = instruction.SelectTypes[0].Validate();
            }
            else
            {
                var secondType = Peek(1);
                var firstType = Peek(2);
                if (secondType.IsReference || firstType.IsReference)
                {
                    throw CreateException("型注釈のないselectの候補には参照型を使えません。", instruction.ByteOffset);
                }
                // Unknownはframeの底側に限られるため、第2候補がUnknownなら第1候補もUnknownになる。
                candidateType = secondType.DeclaredType;
            }

            Pop(WasmValueKind.I32, instruction.ByteOffset);
            Pop(candidateType, instruction.ByteOffset);
            Pop(candidateType, instruction.ByteOffset);

            stack.Add(candidateType);
        }

        void CheckEnd(ControlFrame frame, long byteOffset)
        {
            PopTypes(frame.Results.AsSpan(), byteOffset);
            if (stack.Count != frame.Height)
            {
                throw CreateException("制御の結果の型・個数・順序が宣言と一致しません。", byteOffset);
            }
            // elseとendでは、この制御に入ってから初めて代入したlocalだけを未初期化に戻す。
            while (initializationOrder.Count > frame.InitializedLocalCount)
            // Stryker disable once Block,Statement: ループ本体を空にすると初期化履歴が減らず、巻き戻しが終了しない。
            {
                initializedLocals.Remove(initializationOrder.Pop());
            }
        }

        int AddTarget(ControlFrame frame, int keepCount)
        {
            var index = branchTargets.Count;
            branchTargets.Add(new(0, frame.Height, keepCount));
            return index;
        }

        ControlFrame GetLabel(uint depth, long byteOffset)
        {
            if (depth >= (uint)frames.Count)
            {
                throw CreateUnknownIndex("label", depth, "参照するラベルが存在しません。", byteOffset);
            }
            return frames[frames.Count - 1 - (int)depth];
        }

        int AddLabelTarget(ControlFrame frame)
        {
            var index = AddTarget(frame, frame.LabelTypes.Length);
            if (frame.Kind == ValidationRule.Loop)
            {
                PatchTarget(index, frame.LoopPc);
            }
            else
            {
                frame.ForwardTargets.Add(index);
            }
            return index;
        }

        void PatchTarget(int index, int targetPc)
        {
            branchTargets[index] = branchTargets[index] with { TargetPc = targetPc };
        }

        WasmValueType GetLocalType(uint index, long byteOffset)
        {
            if (index < (uint)type.ParameterTypes.Length)
            {
                return type.ParameterTypes[(int)index];
            }
            var localIndex = index - (uint)type.ParameterTypes.Length;
            var lower = 0;
            var upper = function.Locals.Length;
            // 個数0の宣言を飛ばすため、添字より大きい最初の終端を探す。
            while (lower < upper)
            {
                var middle = lower + (upper - lower) / 2;
                if (localIndex < function.Locals[middle].EndIndex)
                // Stryker disable once Block: 上限の更新を削除すると、同じ中央位置を調べ続けて探索が終了しない。
                {
                    upper = middle;
                }
                else
                // Stryker disable once Block,Arithmetic: 下限の更新を削除するか加算を減算に変えると、探索範囲が狭まらず終了しない。
                {
                    lower = middle + 1;
                }
            }

            if (lower < function.Locals.Length)
            {
                return function.Locals[lower].Type;
            }

            throw CreateUnknownIndex("local", index, "参照するlocalが存在しません。", byteOffset);
        }

        OperandType Peek(int depth)
        {
            var index = stack.Count - 1 - depth;
            return index >= frames[^1].Height ? stack[index] : default;
        }

        OperandType Pop(WasmValueType? expected, long byteOffset)
        {
            var frame = frames[^1];
            if (stack.Count == frame.Height)
            {
                // 現在frameの底だけがUnknownを供給する。具体型は到達不能中も検査する。
                if (frame.Unreachable)
                {
                    return default;
                }

                throw CreateException("命令の入力値が不足しています。", byteOffset);
            }
            var actual = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            CheckType(actual, expected, byteOffset);
            return actual;
        }

        OperandType PopReference(long byteOffset, string operationName = "命令")
        {
            var operand = Pop(null, byteOffset);
            if (operand.DeclaredType is { IsReference: false })
            {
                throw CreateException($"{operationName}の入力には参照型が必要です。", byteOffset);
            }
            return operand;
        }

        void CheckType(OperandType actual, WasmValueType? expected, long byteOffset)
        {
            if (expected is { } expectedType && !actual.Matches(expectedType))
            {
                throw CreateException("命令の入力型が一致しません。", byteOffset);
            }
        }

        void CheckLabelTypes(OperandType[] actual, ImmutableArray<WasmValueType> expected, long byteOffset)
        {
            if (actual.Length != expected.Length)
            {
                throw CreateException("分岐先の引数個数が一致しません。", byteOffset);
            }
            for (var index = 0; index < actual.Length; index++)
            {
                CheckType(actual[index], expected[index], byteOffset);
            }
        }

        void PushTypes(ReadOnlySpan<WasmValueType> kinds)
        {
            foreach (var kind in kinds)
            {
                stack.Add(kind);
            }
        }

        void MarkUnreachable()
        {
            var frame = frames[^1];
            stack.RemoveRange(frame.Height, stack.Count - frame.Height);
            frame.Unreachable = true;
        }

        // 引数と結果は宣言順に積まれるため、末尾から型を照合する。
        void PopTypes(ReadOnlySpan<WasmValueType> kinds, long byteOffset)
        {
            for (var index = kinds.Length - 1; index >= 0; index--)
            {
                Pop(kinds[index], byteOffset);
            }
        }

        WasmValidateException CreateException(string message, long byteOffset)
        {
            return ValidationDiagnostics.ValidationFailure(
                "type mismatch",
                message,
                new(WasmProcessingStage.Validate, byteOffset, moduleFunctionIndex, 10)
            );
        }

        WasmValidateException CreateUnknownIndex(string category, uint index, string message, long byteOffset)
        {
            return ValidationDiagnostics.UnknownIndex(
                category,
                index,
                message,
                new(WasmProcessingStage.Validate, byteOffset, moduleFunctionIndex, 10)
            );
        }
    }

    /// <summary>
    /// 現在の制御で検査できるoperand範囲と結果型を保持する
    /// </summary>
    /// <param name="kind">関数または構造化制御の検証種別</param>
    /// <param name="parameters">宣言順の入口引数型</param>
    /// <param name="results">宣言順の結果型</param>
    /// <param name="height">制御が消費できるoperand範囲の底</param>
    /// <param name="loopPc">loop入口の出力Pc</param>
    /// <param name="initializedLocalCount">制御開始時に初期化済みだった非defaultableな追加localの数</param>
    private sealed class ControlFrame(
        ValidationRule kind,
        ImmutableArray<WasmValueType> parameters,
        ImmutableArray<WasmValueType> results,
        int height,
        int loopPc,
        int initializedLocalCount
    )
    {
        /// <summary>
        /// 関数または構造化制御の検証種別
        /// </summary>
        internal ValidationRule Kind { get; } = kind;

        /// <summary>
        /// 宣言順の入口引数型
        /// </summary>
        internal ImmutableArray<WasmValueType> Parameters { get; } = parameters;

        /// <summary>
        /// 宣言順の結果型
        /// </summary>
        internal ImmutableArray<WasmValueType> Results { get; } = results;

        /// <summary>
        /// loopは入口引数、それ以外は結果を分岐値の型として扱う
        /// </summary>
        internal ImmutableArray<WasmValueType> LabelTypes => Kind == ValidationRule.Loop ? Parameters : Results;

        /// <summary>
        /// 制御が消費できるoperand範囲の底
        /// </summary>
        internal int Height { get; } = height;

        /// <summary>
        /// loop入口の出力Pc
        /// </summary>
        internal int LoopPc { get; } = loopPc;

        /// <summary>
        /// この制御に入る前のlocal初期化履歴の長さ
        /// </summary>
        internal int InitializedLocalCount { get; } = initializedLocalCount;

        /// <summary>
        /// 現在の制御内での到達不能状態
        /// </summary>
        internal bool Unreachable { get; set; }

        /// <summary>
        /// ifのelseを検査しているかどうか
        /// </summary>
        internal bool HasElse { get; set; }

        /// <summary>
        /// 条件0で分岐する入口の分岐情報添字
        /// </summary>
        internal int IfFalseTarget { get; set; }

        /// <summary>
        /// 制御終了時に条件0の分岐先へ設定するelse本体の入口Pc
        /// </summary>
        internal int ElsePc { get; set; }

        /// <summary>
        /// この制御の終了時にPcを確定する分岐情報添字
        /// </summary>
        internal List<int> ForwardTargets { get; } = [];

        /// <summary>
        /// 何も実行しないelseで、引数をそのまま結果として返せるかを判定する
        /// </summary>
        internal bool CanOmitElse()
        {
            return WasmValueType.AcceptsAll(Results, Parameters);
        }
    }
}
