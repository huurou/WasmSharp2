using WasmSharp.Exceptions;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Tests.Reports;

internal class CaseDiagnostic_FromExceptionTests
{
    [Test]
    public async Task リンク不成立の例外を記録する_Messageを加工せず理由と位置とimport識別を保持する()
    {
        // Arrange
        var exception = new WasmInstantiateException(
            "unknown import: \"spectest\" \"print\"  (補足)",
            WasmInstantiateReason.MissingImport,
            2,
            "spectest",
            "print",
            WasmExternalKind.Function,
            new WasmFailureLocation(WasmProcessingStage.Instantiate, 17, null, 2)
        );

        // Act
        var diagnostic = CaseDiagnostic.FromException(
            "instantiate",
            CaseStage.Instantiate,
            exception
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.Operation).IsEqualTo("instantiate");
            await Assert.That(diagnostic.Message).IsEqualTo(exception.Message);
            await Assert.That(diagnostic.Stage).IsEqualTo(CaseStage.Instantiate);
            await Assert
                .That(diagnostic.ExceptionType)
                .IsEqualTo("WasmSharp.Exceptions.WasmInstantiateException");
            await Assert.That(diagnostic.Reason).IsEqualTo("MissingImport");
            await Assert
                .That(diagnostic.Location)
                .IsEqualTo(new FailureLocationRecord("Instantiate", 17, null, 2));
            await Assert
                .That(diagnostic.Import)
                .IsEqualTo(new ImportRecord(2, "spectest", "print", "Function"));
            await Assert.That(diagnostic.Limit).IsNull();
            await Assert.That(diagnostic.Feature).IsNull();
            await Assert.That(diagnostic.UnverifiedRanges).IsEmpty();
            await Assert.That(diagnostic.FromCallback).IsFalse();
        }
    }

    [Test]
    public async Task 未実装の例外を記録する_機能と未確認範囲を保存用の一覧へコピーする()
    {
        // Arrange
        var exception = new WasmUnsupportedFeatureException(
            "SIMD命令は未実装です",
            "simd",
            new WasmFailureLocation(WasmProcessingStage.Validate, 12, 0, 10),
            [new WasmUnverifiedRange(WasmProcessingStage.Validate, 10, 20, "関数本体")]
        );

        // Act
        var diagnostic = CaseDiagnostic.FromException("validate", CaseStage.Validate, exception);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.Message).IsEqualTo("SIMD命令は未実装です");
            await Assert.That(diagnostic.Feature).IsEqualTo("simd");
            await Assert
                .That(
                    diagnostic.UnverifiedRanges.SequenceEqual([new("Validate", 10, 20, "関数本体")])
                )
                .IsTrue();
            await Assert
                .That(diagnostic.Location)
                .IsEqualTo(new FailureLocationRecord("Validate", 12, 0, 10));
            await Assert.That(diagnostic.Reason).IsNull();
            await Assert.That(diagnostic.Import).IsNull();
        }
    }

    [Test]
    [Arguments("exhaustion", "CallDepthLimit", 1024)]
    [Arguments("implementation_limit", "CollectionSize", 100)]
    [Arguments("trap", "Unreachable", null)]
    public async Task 上限到達や実装上限やtrapの例外を記録する_理由と上限値と位置を保持する(
        string kind,
        string reason,
        int? limit
    )
    {
        // Arrange
        var location = new WasmFailureLocation(WasmProcessingStage.Invoke, null, 3);
        Exception exception = kind switch
        {
            "exhaustion" => new WasmExhaustionException(
                "call stack exhausted",
                WasmExhaustionReason.CallDepthLimit,
                1024,
                location
            ),
            "implementation_limit" => new WasmImplementationLimitException(
                "too many elements",
                WasmImplementationLimitReason.CollectionSize,
                100,
                location
            ),
            _ => new WasmTrapException("unreachable", WasmTrapReason.Unreachable, location),
        };

        // Act
        var diagnostic = CaseDiagnostic.FromException("invoke", CaseStage.Invoke, exception);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.Reason).IsEqualTo(reason);
            await Assert.That(diagnostic.Limit).IsEqualTo(limit);
            await Assert
                .That(diagnostic.Location)
                .IsEqualTo(new FailureLocationRecord("Invoke", null, 3, null));
        }
    }

    [Test]
    public async Task Import情報の取得失敗を記録する_理由と機能と未確認範囲を保持する()
    {
        // Arrange
        var exception = new WasmImportInspectionException(
            "未対応のsectionです",
            WasmImportInspectionReason.UnsupportedFeature,
            "gc",
            new WasmFailureLocation(WasmProcessingStage.Decode, 8, null, 1),
            [new WasmUnverifiedRange(WasmProcessingStage.Decode, 8, 40, "type section")],
            null
        );

        // Act
        var diagnostic = CaseDiagnostic.FromException(
            "inspect_imports",
            CaseStage.InspectImports,
            exception
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.Stage).IsEqualTo(CaseStage.InspectImports);
            await Assert.That(diagnostic.Reason).IsEqualTo("UnsupportedFeature");
            await Assert.That(diagnostic.Feature).IsEqualTo("gc");
            await Assert
                .That(
                    diagnostic.UnverifiedRanges.SequenceEqual([
                        new("Decode", 8, 40, "type section"),
                    ])
                )
                .IsTrue();
            await Assert
                .That(diagnostic.Location)
                .IsEqualTo(new FailureLocationRecord("Decode", 8, null, 1));
        }
    }

    [Test]
    public async Task ホストcallbackの例外を記録する_完全型名とcallback由来を保持しWasmの診断情報を持たない()
    {
        // Arrange
        var exception = new InvalidOperationException("callback failed");

        // Act
        var diagnostic = CaseDiagnostic.FromException(
            "invoke",
            CaseStage.Invoke,
            exception,
            fromCallback: true
        );

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.Message).IsEqualTo("callback failed");
            await Assert
                .That(diagnostic.ExceptionType)
                .IsEqualTo("System.InvalidOperationException");
            await Assert.That(diagnostic.FromCallback).IsTrue();
            await Assert.That(diagnostic.Reason).IsNull();
            await Assert.That(diagnostic.Limit).IsNull();
            await Assert.That(diagnostic.Location).IsNull();
            await Assert.That(diagnostic.Import).IsNull();
            await Assert.That(diagnostic.Feature).IsNull();
            await Assert.That(diagnostic.UnverifiedRanges).IsEmpty();
        }
    }
}
