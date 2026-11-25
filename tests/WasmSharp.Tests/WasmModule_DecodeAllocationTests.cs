using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_DecodeTests
{
    [Test]
    public async Task Core2の関数型に短い引数列と結果列を加える_追加割り当ては同じ関数型を作る費用に収まる()
    {
        // Arrange
        const int DeclarationCount = 32;
        byte[] empty = [];
        byte[] parameterBytes = [0x7F, 0x7F];
        byte[] resultBytes = [0x7F];
        var noArgumentsOrResults = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types([.. Enumerable.Repeat((empty, empty), DeclarationCount)])
        );
        var typedModule = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Types([.. Enumerable.Repeat((parameterBytes, resultBytes), DeclarationCount)])
        );
        WasmValueKind[] parameters = [WasmValueKind.I32, WasmValueKind.I32];
        WasmValueKind[] results = [WasmValueKind.I32];
        var functions = new WasmFunctionType[DeclarationCount];
        MeasureDecodeAllocation(noArgumentsOrResults, WasmSpecVersion.Core2);
        MeasureDecodeAllocation(typedModule, WasmSpecVersion.Core2);
        MeasureFunctionTypes();

        // Act
        var emptyAllocation = MeasureDecodeAllocation(noArgumentsOrResults, WasmSpecVersion.Core2);
        var typedAllocation = MeasureDecodeAllocation(typedModule, WasmSpecVersion.Core2);
        var constructionAllocation = MeasureFunctionTypes();

        // Assert
        // 宣言数をそろえて引数・結果を加えた分だけを測り、同じランタイムで関数型を構築する費用と比較する。
        await Assert.That(typedAllocation - emptyAllocation).IsLessThanOrEqualTo(constructionAllocation);

        long MeasureFunctionTypes()
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < functions.Length; index++)
            {
                functions[index] = new WasmFunctionType(parameters, results);
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(functions);
            return allocated;
        }
    }

    [Test]
    public async Task 上位型を1件持つ関数型をデコードする_同じ型を引数に1件持つ場合の割り当てに収まる()
    {
        // Arrange
        // どちらも型0を継承する型1を先に宣言し、上位型を記録するための初回の割り当てをそろえる。
        byte[] baseTypes = [0x50, 0, 0x60, 0, 0, 0x50, 1, 0, 0x60, 0, 0];
        var withSupertype = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [3, .. baseTypes, 0x4F, 1, 1, 0x60, 0, 0])
        );
        var withParameter = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(1, [3, .. baseTypes, 0x4F, 0, 0x60, 1, 0x64, 1, 0])
        );
        for (var index = 0; index < 4; index++)
        {
            MeasureDecodeAllocation(withSupertype);
            MeasureDecodeAllocation(withParameter);
        }

        // Act
        var supertypeAllocation = MeasureDecodeAllocation(withSupertype);
        var parameterAllocation = MeasureDecodeAllocation(withParameter);

        // Assert
        // 最後の関数型はどちらも型1への参照を1件保持する。引数側はその値型の種別も配列に保持する。
        await Assert.That(supertypeAllocation).IsLessThanOrEqualTo(parameterAllocation);
    }

    [Test]
    public async Task 通常のfuncrefの定数を繰り返しデコードする_同じ長さの数値定数列に比べて命令ごとの割当が増えない()
    {
        // Arrange
        var numbers = CreateConstantSequence([0x41, 0, 0x1A]); // i32.const 0、drop
        var references = CreateConstantSequence([0xD0, 0x70, 0x1A]); // ref.null func、drop
        for (var index = 0; index < 4; index++)
        {
            MeasureDecodeAllocation(numbers);
            MeasureDecodeAllocation(references);
        }

        // Act
        var numberAllocation = MeasureDecodeAllocation(numbers);
        var referenceAllocation = MeasureDecodeAllocation(references);

        // Assert
        await Assert.That(referenceAllocation).IsLessThanOrEqualTo(numberAllocation);
    }

    private static byte[] CreateConstantSequence(byte[] instructions)
    {
        // 命令数、即値の長さ、戻り値をそろえ、型の表現による割当の差だけを測る。
        return ConstantModuleBinary.Create(
            0x7F,
            [.. Enumerable.Repeat(instructions, 256).SelectMany(x => x), 0x41, 0, 0x0B]
        );
    }

    private static long MeasureDecodeAllocation(byte[] binary, WasmSpecVersion version = WasmSpecVersion.Core3)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(WasmModule.Decode(binary, version));
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
