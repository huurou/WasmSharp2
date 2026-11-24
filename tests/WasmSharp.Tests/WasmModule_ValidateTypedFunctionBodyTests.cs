using WasmSharp.Exceptions;
using WasmSharp.Tests.Fixtures;

namespace WasmSharp.Tests;

internal partial class WasmModule_ValidateTests
{
    [Test]
    public async Task 型付き関数参照をselectとlocalとblockと直接呼び出しに渡す_選んだ関数またはnullを返す()
    {
        // Arrange
        var types = HostLinkingModuleBinary.Section(
            1,
            [
                3,
                0x60,
                0,
                0, // () -> ()
                0x60,
                1,
                0x63,
                0,
                1,
                0x63,
                0, // (ref null 0) -> (ref null 0)
                0x60,
                1,
                0x7F,
                1,
                0x63,
                0, // i32 -> (ref null 0)
            ]
        );
        byte[] chooseBody =
        [
            1,
            1,
            0x63,
            0, // (local (ref null 0))
            0x02,
            0x63,
            0, // block (result (ref null 0))
            0xD2,
            0, // ref.func 0
            0xD0,
            0, // ref.null 0
            0x20,
            0, // local.get 0
            0x1C,
            1,
            0x63,
            0, // select (result (ref null 0))
            0x10,
            1, // call 1
            0x21,
            1, // local.set 1
            0x20,
            1, // local.get 1
            0x0B,
            0x0B,
        ];
        var code = HostLinkingModuleBinary.Section(
            10,
            [3, 2, 0, 0x0B, 4, 0, 0x20, 0, 0x0B, .. HostLinkingModuleBinary.Unsigned((uint)chooseBody.Length), .. chooseBody]
        );
        var binary = HostLinkingModuleBinary.Create(
            types,
            HostLinkingModuleBinary.Functions(0, 1, 2),
            HostLinkingModuleBinary.Exports(("target", 0, 0u), ("choose", 0, 2u)),
            code
        );
        var context = new WasmExecutionContext();

        // Act
        var instance = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        var choose = instance.GetFunction("choose");
        var first = choose.Invoke(context, [WasmValue.FromI32(1)]).Values[0];
        var second = choose.Invoke(context, [WasmValue.FromI32(0)]).Values[0];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.AsFuncRef()).IsSameReferenceAs(instance.GetFunction("target"));
            await Assert.That(second.AsFuncRef()).IsNull();
        }
    }

    [Test]
    [Arguments("D0000B")]
    [Arguments("D2010B")]
    [Arguments("20000B")]
    public async Task 非nullの具体的な関数型を返せない_型不一致として検証を拒否する(string body)
    {
        // Arrange
        var types = HostLinkingModuleBinary.Section(
            1,
            [
                3,
                0x60,
                0,
                0, // () -> ()
                0x60,
                0,
                1,
                0x7F, // () -> i32
                0x60,
                1,
                0x64,
                0x70,
                1,
                0x64,
                0, // (ref func) -> (ref 0)
            ]
        );
        var binary = HostLinkingModuleBinary.Create(
            types,
            HostLinkingModuleBinary.Functions(0, 1, 2),
            HostLinkingModuleBinary.Exports(("other", 0, 1u)),
            HostLinkingModuleBinary.Code(([], [0x0B]), ([], [0x41, 0, 0x0B]), ([], Convert.FromHexString(body)))
        );
        var module = WasmModule.Decode(binary, WasmSpecVersion.Core3);

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("type mismatch");
            await Assert.That(exception.Location?.FunctionIndex).IsEqualTo(2u);
        }
    }

    [Test]
    public async Task 条件分岐でelseを省略したifの引数が結果の参照型に適合する_条件によらず同じ関数を返す()
    {
        // Arrange
        var binary = HostLinkingModuleBinary.Create(
            HostLinkingModuleBinary.Section(
                1,
                [
                    3,
                    0x60,
                    0,
                    0, // () -> ()
                    0x60,
                    1,
                    0x64,
                    0,
                    1,
                    0x63,
                    0, // (ref 0) -> (ref null 0)
                    0x60,
                    1,
                    0x7F,
                    1,
                    0x63,
                    0, // i32 -> (ref null 0)
                ]
            ),
            HostLinkingModuleBinary.Functions(0, 2),
            HostLinkingModuleBinary.Exports(("target", 0, 0u), ("choose", 0, 1u)),
            HostLinkingModuleBinary.Code(([], [0x0B]), ([], [0xD2, 0, 0x20, 0, 0x04, 1, 0x0B, 0x0B]))
        );
        var context = new WasmExecutionContext();

        // Act
        var instance = WasmModule.Decode(binary, WasmSpecVersion.Core3).Validate().Instantiate(context, []);
        var choose = instance.GetFunction("choose");
        var first = choose.Invoke(context, [WasmValue.FromI32(1)]).Values[0];
        var second = choose.Invoke(context, [WasmValue.FromI32(0)]).Values[0];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.AsFuncRef()).IsSameReferenceAs(instance.GetFunction("target"));
            await Assert.That(second.AsFuncRef()).IsSameReferenceAs(instance.GetFunction("target"));
        }
    }

    [Test]
    public async Task 既定値のない追加localを代入前に読む_検証時に未初期化を報告する()
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Section(10, [1, 8, 1, 1, 0x64, 0x6F, 0x20, 0, 0x1A, 0x0B])
            ),
            WasmSpecVersion.Core3
        );

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("uninitialized local");
            await Assert.That(exception.Message).Contains("初期化していないlocal");
            await Assert.That(exception.Location?.Stage).IsEqualTo(WasmProcessingStage.Validate);
        }
    }

    [Test]
    public async Task 個数0の追加localが存在しない型番号を使う_追加されるlocalだけを検証する()
    {
        // Arrange
        var module = ReferenceControlModule.Create(
            [1, 0x60, 0, 1, 0x7F],
            [0x20, 0], // local.get 0
            locals: [2, 1, 0x7F, 0, 0x63, 1] // i32を1個、(ref null 1)を0個宣言する
        );

        // Act
        var validated = module.Validate();

        // Assert
        await Assert.That(validated).IsSameReferenceAs(module);
    }

    [Test]
    [Arguments("00026301000B1A0B")]
    [Arguments("00001C0163011A0B")]
    [Arguments("00D0011A0B")]
    [Arguments("010163010B")]
    public async Task 制御型とselectとref_nullと追加localが未知の型添字を使う_検証時に型番号を報告する(string body)
    {
        // Arrange
        var bytes = Convert.FromHexString(body);
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Section(10, [1, .. HostLinkingModuleBinary.Unsigned((uint)bytes.Length), .. bytes])
            ),
            WasmSpecVersion.Core3
        );

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Message).StartsWith("unknown type 1");
            await Assert.That(exception.Location?.FunctionIndex).IsEqualTo(0u);
        }
    }

    [Test]
    [Arguments("FC0C0000")]
    [Arguments("FC0E0001")]
    public async Task 抽象参照型funcrefの要素を具体的な関数型のテーブルへ転送する_件数が0でも検証時に拒否する(
        string operation
    )
    {
        // Arrange
        var module = WasmModule.Decode(
            HostLinkingModuleBinary.Create(
                HostLinkingModuleBinary.Types(([], [])),
                HostLinkingModuleBinary.Imports(("env", "typed", 1, [0x63, 0, 0, 0]), ("env", "wide", 1, [0x70, 0, 0])),
                HostLinkingModuleBinary.Functions(0),
                HostLinkingModuleBinary.Section(9, [1, 5, 0x70, 0]),
                HostLinkingModuleBinary.Code(([], [0x41, 0, 0x41, 0, 0x41, 0, .. Convert.FromHexString(operation), 0x0B]))
            ),
            WasmSpecVersion.Core3
        );

        // Act & Assert
        var exception = await Assert.That(() => module.Validate()).ThrowsExactly<WasmValidateException>();
        await Assert.That(exception!.Message).StartsWith("type mismatch");
    }
}
