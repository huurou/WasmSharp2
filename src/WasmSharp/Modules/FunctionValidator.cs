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
    /// <param name="functionTypes">importを先頭とする関数index順の解決済みの型</param>
    /// <param name="globalTypes">importを先頭とするglobal index順の解決済みの型</param>
    /// <param name="references">当該module検証で共有する参照情報</param>
    /// <returns>実行命令と確定済み分岐情報、追加localsとoperandの最大要素数を持つ、この定義関数の実行コード</returns>
    /// <exception cref="WasmValidateException">命令の参照先、globalの可変性、入力型または結果の型・個数・順序が不正な場合</exception>
    /// <exception cref="InvalidOperationException">デコード済み命令の記述子や検証規則が実装内で一致しない場合</exception>
    internal static FunctionCode Validate(
        WasmModule module,
        int definitionIndex,
        uint moduleFunctionIndex,
        WasmFunctionType[] functionTypes,
        WasmGlobalType[] globalTypes,
        ReferenceValidationContext references
    )
    {
        const int STACKALLOC_DECLARATION_LIMIT = 128;

        var function = module.Functions[definitionIndex];
        var type = functionTypes[(int)moduleFunctionIndex];
        // 圧縮宣言ごとの累積終端を使い、localの個数に比例する配列は作らない。
        var localEnds =
            function.Locals.Length <= STACKALLOC_DECLARATION_LIMIT
                ? stackalloc ulong[function.Locals.Length]
                : new ulong[function.Locals.Length];
        ulong localCount = 0;
        for (var index = 0; index < function.Locals.Length; index++)
        {
            localCount += function.Locals[index].Count;
            localEnds[index] = localCount;
        }
        var instructions = ImmutableArray.CreateBuilder<Instruction>(function.Instructions.Length);
        var branchTargets = ImmutableArray.CreateBuilder<BranchTarget>();
        List<WasmValueKind?> stack = [];
        List<ControlFrame> frames = [new(ValidationRule.FunctionEnd, [], type.Results, 0, 0)];
        var maxOperandStack = 0;
        var hasMemory =
            !module.Memories.IsEmpty || module.Imports.Any(x => x.Kind == WasmExternalKind.Memory);
        foreach (var instruction in function.Instructions)
        {
            if (!InstructionSet.TryGet(instruction.Opcode, out var descriptor))
            {
                throw new InvalidOperationException("デコード済み命令の情報がありません。");
            }

            var emitInstruction = true;
            var outputIndex = instruction.Index;
            var outputTargetCount = 0;

            switch (descriptor.Validation)
            {
                case ValidationRule.None:
                    break;

                case ValidationRule.Block:
                case ValidationRule.Loop:
                case ValidationRule.If:
                    var (parameters, results) = ResolveBlockType(
                        instruction.BlockType,
                        instruction.ByteOffset
                    );
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
                        instructions.Count
                    );
                    frames.Add(enteredFrame);
                    PushTypes(parameters);
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
                    PushTypes(ifFrame.Parameters);
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
                            StackEffectKind.PushReference => instruction.Immediate.Kind,
                            _ => throw new InvalidOperationException(
                                "定数命令のスタック効果が不正です。"
                            ),
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
                            new(
                                WasmProcessingStage.Validate,
                                instruction.ByteOffset,
                                moduleFunctionIndex,
                                10
                            )
                        );
                    }
                    stack.Add(WasmValueKind.FuncRef);
                    break;

                case ValidationRule.RefIsNull:
                    var referenceType = Pop(null, instruction.ByteOffset);
                    if (
                        referenceType is { }
                        && referenceType is not (WasmValueKind.FuncRef or WasmValueKind.ExternRef)
                    )
                    {
                        throw CreateException(
                            "null判定の入力には参照型が必要です。",
                            instruction.ByteOffset
                        );
                    }
                    stack.Add(WasmValueKind.I32);
                    break;

                case ValidationRule.Numeric:
                    switch (descriptor.Immediate)
                    {
                        case ImmediateKind.LaneIndex:
                            ValidateLaneIndex(
                                instruction.LaneIndex,
                                descriptor.LaneCount,
                                instruction.ByteOffset
                            );
                            break;
                        case ImmediateKind.Shuffle:
                            foreach (var laneIndex in instruction.ShuffleLanes)
                            {
                                ValidateLaneIndex(laneIndex, 32, instruction.ByteOffset);
                            }
                            break;
                    }
                    var numericSignature =
                        descriptor.NumericSignature
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
                    var memorySignature = ValidateMemoryAccess(instruction, descriptor);
                    if (
                        descriptor.Validation
                        is ValidationRule.MemoryLoadLane
                            or ValidationRule.MemoryStoreLane
                    )
                    {
                        ValidateLaneIndex(
                            instruction.LaneIndex,
                            descriptor.LaneCount,
                            instruction.ByteOffset
                        );
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
                    Pop(WasmValueKind.I32, instruction.ByteOffset);
                    if (
                        descriptor.Validation
                        is ValidationRule.MemoryLoad
                            or ValidationRule.MemoryLoadLane
                    )
                    {
                        stack.Add(memorySignature.ValueType);
                    }
                    break;

                case ValidationRule.MemorySize:
                case ValidationRule.MemoryGrow:
                    RequireMemory(instruction.ByteOffset);
                    if (descriptor.Validation == ValidationRule.MemoryGrow)
                    {
                        Pop(WasmValueKind.I32, instruction.ByteOffset);
                    }
                    stack.Add(WasmValueKind.I32);
                    break;

                case ValidationRule.MemoryBulk:
                    RequireMemory(instruction.ByteOffset);
                    PopThreeI32(instruction.ByteOffset);
                    break;

                case ValidationRule.MemoryInit:
                case ValidationRule.DataDrop:
                    if (descriptor.Validation == ValidationRule.MemoryInit)
                    {
                        RequireMemory(instruction.ByteOffset);
                    }
                    if (instruction.Index >= (uint)module.DataSegments.Length)
                    {
                        throw CreateUnknownIndex(
                            "data segment",
                            instruction.Index,
                            "参照するデータセグメントが存在しません。",
                            instruction.ByteOffset
                        );
                    }
                    if (descriptor.Validation == ValidationRule.MemoryInit)
                    {
                        PopThreeI32(instruction.ByteOffset);
                    }
                    break;

                case ValidationRule.TableGet:
                case ValidationRule.TableSet:
                case ValidationRule.TableGrow:
                case ValidationRule.TableSize:
                case ValidationRule.TableFill:
                    var tableElementType = GetTableElementType(
                        instruction.Index,
                        instruction.ByteOffset
                    );
                    switch (descriptor.Validation)
                    {
                        case ValidationRule.TableGet:
                            Pop(WasmValueKind.I32, instruction.ByteOffset);
                            stack.Add(tableElementType);
                            break;
                        case ValidationRule.TableSet:
                            Pop(tableElementType, instruction.ByteOffset);
                            Pop(WasmValueKind.I32, instruction.ByteOffset);
                            break;
                        case ValidationRule.TableGrow:
                            Pop(WasmValueKind.I32, instruction.ByteOffset);
                            Pop(tableElementType, instruction.ByteOffset);
                            stack.Add(WasmValueKind.I32);
                            break;
                        case ValidationRule.TableSize:
                            stack.Add(WasmValueKind.I32);
                            break;
                        case ValidationRule.TableFill:
                            Pop(WasmValueKind.I32, instruction.ByteOffset);
                            Pop(tableElementType, instruction.ByteOffset);
                            Pop(WasmValueKind.I32, instruction.ByteOffset);
                            break;
                    }
                    break;

                case ValidationRule.TableCopy:
                    var destinationType = GetTableElementType(
                        instruction.Index,
                        instruction.ByteOffset
                    );
                    var sourceType = GetTableElementType(
                        instruction.SecondaryIndex,
                        instruction.ByteOffset
                    );
                    if (destinationType != sourceType)
                    {
                        throw CreateException(
                            "コピー元とコピー先のテーブルの参照型が一致しません。",
                            instruction.ByteOffset
                        );
                    }
                    PopThreeI32(instruction.ByteOffset);
                    break;

                case ValidationRule.TableInit:
                    // バイナリの即値順と分け、テーブル、要素、型、operandの順に検査する。
                    var initializationTableType = GetTableElementType(
                        instruction.SecondaryIndex,
                        instruction.ByteOffset
                    );
                    var elementType = GetElementType(instruction.Index, instruction.ByteOffset);
                    if (initializationTableType != elementType)
                    {
                        throw CreateException(
                            "要素セグメントと対象テーブルの参照型が一致しません。",
                            instruction.ByteOffset
                        );
                    }
                    PopThreeI32(instruction.ByteOffset);
                    break;

                case ValidationRule.ElementDrop:
                    GetElementType(instruction.Index, instruction.ByteOffset);
                    break;

                case ValidationRule.FunctionEnd:
                    var endedFrame = frames[^1];
                    CheckEnd(endedFrame, instruction.ByteOffset);
                    if (endedFrame.Kind == ValidationRule.If)
                    {
                        if (
                            !endedFrame.HasElse
                            && !endedFrame
                                .Parameters.AsSpan()
                                .SequenceEqual(endedFrame.Results.AsSpan())
                        )
                        {
                            throw CreateException(
                                "elseを省略したifの引数型と結果型が一致しません。",
                                instruction.ByteOffset
                            );
                        }
                        PatchTarget(
                            endedFrame.IfFalseTarget,
                            endedFrame.HasElse ? endedFrame.ElsePc : instructions.Count
                        );
                    }
                    // 関数frameの前方参照も、配列末尾ではなく末尾ReturnのPcへ接続する。
                    foreach (var targetIndex in endedFrame.ForwardTargets)
                    {
                        PatchTarget(targetIndex, instructions.Count);
                    }
                    frames.RemoveAt(frames.Count - 1);
                    if (endedFrame.Kind != ValidationRule.FunctionEnd)
                    {
                        PushTypes(endedFrame.Results);
                        emitInstruction = false;
                    }
                    break;

                case ValidationRule.Call:
                    if (instruction.Index >= (uint)functionTypes.Length)
                    {
                        throw CreateUnknownIndex(
                            "function",
                            instruction.Index,
                            "参照する関数が存在しません。",
                            instruction.ByteOffset
                        );
                    }
                    var calleeType = functionTypes[(int)instruction.Index];
                    PopTypes(calleeType.Parameters.AsSpan(), instruction.ByteOffset);
                    PushTypes(calleeType.Results);
                    break;

                case ValidationRule.CallIndirect:
                    // バイナリの即値順と分け、テーブル、型、funcref制約、operandの順に検査する。
                    var indirectTableType = GetTableElementType(
                        instruction.SecondaryIndex,
                        instruction.ByteOffset
                    );
                    if (instruction.Index >= (uint)module.Types.Length)
                    {
                        throw CreateUnknownIndex(
                            "type",
                            instruction.Index,
                            "参照する型が存在しません。",
                            instruction.ByteOffset
                        );
                    }
                    if (indirectTableType != WasmValueKind.FuncRef)
                    {
                        throw CreateException(
                            "間接呼び出し先のテーブルにはfuncrefが必要です。",
                            instruction.ByteOffset
                        );
                    }
                    var indirectCalleeType = module.Types[(int)instruction.Index];
                    Pop(WasmValueKind.I32, instruction.ByteOffset);
                    PopTypes(indirectCalleeType.Parameters.AsSpan(), instruction.ByteOffset);
                    PushTypes(indirectCalleeType.Results);
                    break;

                case ValidationRule.Return:
                    PopTypes(type.Results.AsSpan(), instruction.ByteOffset);
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
                        PushTypes(labelFrame.LabelTypes);
                    }
                    else
                    {
                        MarkUnreachable();
                    }
                    break;

                case ValidationRule.BranchTable:
                    var defaultLabel = GetLabel(instruction.Labels[^1], instruction.ByteOffset);
                    var branchTypes = new WasmValueKind?[defaultLabel.LabelTypes.Length];
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
                        var candidateLabel = GetLabel(
                            instruction.Labels[index],
                            instruction.ByteOffset
                        );
                        CheckLabelTypes(
                            branchTypes,
                            candidateLabel.LabelTypes,
                            instruction.ByteOffset
                        );
                        AddLabelTarget(candidateLabel);
                    }
                    AddLabelTarget(defaultLabel);
                    Pop(WasmValueKind.I32, instruction.ByteOffset);
                    for (var index = branchTypes.Length - 1; index >= 0; index--)
                    {
                        Pop(branchTypes[index], instruction.ByteOffset);
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
                        stack.Add(globalType.ValueKind);
                    }
                    else
                    {
                        if (!globalType.IsMutable)
                        {
                            throw ValidationDiagnostics.ValidationFailure(
                                "global is immutable",
                                "immutable globalは更新できません。",
                                new(
                                    WasmProcessingStage.Validate,
                                    instruction.ByteOffset,
                                    moduleFunctionIndex,
                                    10
                                )
                            );
                        }
                        Pop(globalType.ValueKind, instruction.ByteOffset);
                    }
                    break;

                case ValidationRule.Drop:
                    Pop(null, instruction.ByteOffset);
                    break;

                case ValidationRule.Select:
                case ValidationRule.TypedSelect:
                    ValidateSelect(
                        instruction,
                        descriptor.Validation == ValidationRule.TypedSelect
                    );
                    break;

                case ValidationRule.LocalGet:
                case ValidationRule.LocalSet:
                case ValidationRule.LocalTee:
                    var localType = GetLocalType(
                        instruction.Index,
                        instruction.ByteOffset,
                        localEnds
                    );
                    if (descriptor.Validation != ValidationRule.LocalGet)
                    {
                        Pop(localType, instruction.ByteOffset);
                    }
                    if (descriptor.Validation != ValidationRule.LocalSet)
                    {
                        stack.Add(localType);
                    }
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
                        instruction.SecondaryIndex,
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
            branchTargets.ToImmutable()
        );

        void PopThreeI32(long byteOffset)
        {
            Pop(WasmValueKind.I32, byteOffset);
            Pop(WasmValueKind.I32, byteOffset);
            Pop(WasmValueKind.I32, byteOffset);
        }

        WasmValueKind GetTableElementType(uint index, long byteOffset)
        {
            if (index >= (uint)references.TableElementTypes.Length)
            {
                throw CreateUnknownIndex(
                    "table",
                    index,
                    "参照するテーブルが存在しません。",
                    byteOffset
                );
            }
            return references.TableElementTypes[(int)index];
        }

        WasmValueKind GetElementType(uint index, long byteOffset)
        {
            if (index >= (uint)references.ElementTypes.Length)
            {
                throw CreateUnknownIndex(
                    "elem segment",
                    index,
                    "参照する要素セグメントが存在しません。",
                    byteOffset
                );
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

        void RequireMemory(long byteOffset)
        {
            if (!hasMemory)
            {
                throw CreateUnknownIndex("memory", 0, "参照するメモリが存在しません。", byteOffset);
            }
        }

        MemoryAccessSignature ValidateMemoryAccess(
            DecodedInstruction instruction,
            InstructionDescriptor descriptor
        )
        {
            RequireMemory(instruction.ByteOffset);
            var memorySignature =
                descriptor.MemoryAccessSignature
                ?? throw new InvalidOperationException("メモリ命令のアクセス情報がありません。");
            if (
                instruction.MemoryArgument.Alignment
                > (uint)BitOperations.Log2(memorySignature.ByteWidth)
            )
            {
                throw ValidationDiagnostics.ValidationFailure(
                    "alignment must not be larger than natural",
                    "アラインメントが命令のアクセス幅を超えています。",
                    new(
                        WasmProcessingStage.Validate,
                        instruction.ByteOffset,
                        moduleFunctionIndex,
                        10
                    )
                );
            }
            return memorySignature;
        }

        (
            ImmutableArray<WasmValueKind> Parameters,
            ImmutableArray<WasmValueKind> Results
        ) ResolveBlockType(BlockType blockType, long byteOffset)
        {
            switch (blockType.Kind)
            {
                case BlockTypeKind.Empty:
                    return ([], []);
                case BlockTypeKind.Value:
                    return ([], [blockType.ValueType]);
                case BlockTypeKind.TypeIndex:
                    if (blockType.TypeIndex >= (uint)module.Types.Length)
                    {
                        throw CreateUnknownIndex(
                            "type",
                            blockType.TypeIndex,
                            "参照する型が存在しません。",
                            byteOffset
                        );
                    }
                    var blockSignature = module.Types[(int)blockType.TypeIndex];
                    return (blockSignature.Parameters, blockSignature.Results);
                default:
                    throw new InvalidOperationException("制御の型指定が不正です。");
            }
        }

        void ValidateSelect(DecodedInstruction instruction, bool typed)
        {
            WasmValueKind? candidateType;
            if (typed)
            {
                // 型vector長は、候補の型やoperand不足より先に検査する。
                if (instruction.SelectTypes.Length != 1)
                {
                    throw ValidationDiagnostics.ValidationFailure(
                        "invalid result arity",
                        "型注釈付きselectの結果型は1個である必要があります。",
                        new(
                            WasmProcessingStage.Validate,
                            instruction.ByteOffset,
                            moduleFunctionIndex,
                            10
                        )
                    );
                }
                candidateType = instruction.SelectTypes[0];
            }
            else
            {
                var secondType = Peek(1);
                var firstType = Peek(2);
                if (
                    secondType is WasmValueKind.FuncRef or WasmValueKind.ExternRef
                    || firstType is WasmValueKind.FuncRef or WasmValueKind.ExternRef
                )
                {
                    throw CreateException(
                        "型注釈のないselectの候補には参照型を使えません。",
                        instruction.ByteOffset
                    );
                }
                // 具体候補に適合させ、両方Unknownの場合だけ結果もUnknownにする。
                candidateType = secondType ?? firstType;
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
                throw CreateException(
                    "制御の結果の型・個数・順序が宣言と一致しません。",
                    byteOffset
                );
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
                throw CreateUnknownIndex(
                    "label",
                    depth,
                    "参照するラベルが存在しません。",
                    byteOffset
                );
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

        WasmValueKind GetLocalType(uint index, long byteOffset, ReadOnlySpan<ulong> ends)
        {
            if (index < (uint)type.Parameters.Length)
            {
                return type.Parameters[(int)index];
            }
            var localIndex = index - (uint)type.Parameters.Length;
            var lower = 0;
            var upper = ends.Length;
            // 個数0の宣言を飛ばすため、添字より大きい最初の終端を探す。
            while (lower < upper)
            {
                var middle = lower + (upper - lower) / 2;
                if (localIndex < ends[middle])
                {
                    upper = middle;
                }
                else
                {
                    lower = middle + 1;
                }
            }

            if (lower < ends.Length)
            {
                return function.Locals[lower].Type;
            }

            throw CreateUnknownIndex("local", index, "参照するlocalが存在しません。", byteOffset);
        }

        WasmValueKind? Peek(int depth)
        {
            var index = stack.Count - 1 - depth;
            return index >= frames[^1].Height ? stack[index] : null;
        }

        WasmValueKind? Pop(WasmValueKind? expected, long byteOffset)
        {
            var frame = frames[^1];
            if (stack.Count == frame.Height)
            {
                // 現在frameの底だけがUnknownを供給する。具体型は到達不能中も検査する。
                if (frame.Unreachable)
                {
                    return null;
                }

                throw CreateException("命令の入力値が不足しています。", byteOffset);
            }
            var actual = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            CheckType(actual, expected, byteOffset);
            return actual;
        }

        void CheckType(WasmValueKind? actual, WasmValueKind? expected, long byteOffset)
        {
            if (expected is { } kind && actual is { } actualKind && actualKind != kind)
            {
                throw CreateException("命令の入力型が一致しません。", byteOffset);
            }
        }

        void CheckLabelTypes(
            WasmValueKind?[] actual,
            ImmutableArray<WasmValueKind> expected,
            long byteOffset
        )
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

        void PushTypes(ImmutableArray<WasmValueKind> kinds)
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
        void PopTypes(ReadOnlySpan<WasmValueKind> kinds, long byteOffset)
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

        WasmValidateException CreateUnknownIndex(
            string category,
            uint index,
            string message,
            long byteOffset
        )
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
    private sealed class ControlFrame(
        ValidationRule kind,
        ImmutableArray<WasmValueKind> parameters,
        ImmutableArray<WasmValueKind> results,
        int height,
        int loopPc
    )
    {
        /// <summary>
        /// 関数または構造化制御の検証種別
        /// </summary>
        internal ValidationRule Kind { get; } = kind;

        /// <summary>
        /// 宣言順の入口引数型
        /// </summary>
        internal ImmutableArray<WasmValueKind> Parameters { get; } = parameters;

        /// <summary>
        /// 宣言順の結果型
        /// </summary>
        internal ImmutableArray<WasmValueKind> Results { get; } = results;

        /// <summary>
        /// loopは入口引数、それ以外は結果を分岐値の型として扱う
        /// </summary>
        internal ImmutableArray<WasmValueKind> LabelTypes =>
            Kind == ValidationRule.Loop ? Parameters : Results;

        /// <summary>
        /// 制御が消費できるoperand範囲の底
        /// </summary>
        internal int Height { get; } = height;

        /// <summary>
        /// loop入口の出力Pc
        /// </summary>
        internal int LoopPc { get; } = loopPc;

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
    }
}
