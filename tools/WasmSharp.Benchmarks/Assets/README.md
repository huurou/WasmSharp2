# 素材とケースの固定条件

タスク2.1〜2.4で追加する6素材・13ケース。固定値は`cases.json`の既存`BenchmarkCase`形式で保持する。6素材・15ケースへの統合とmanifest生成、小moduleの準備2ケースはタスク2.5、ケース読込・公開APIの検査器はタスク3.1・3.2で接続する。

Invokeの10ケースは1回の公開Invokeを1操作とする。loopの4096回はguestの仕事量であり、操作数ではない。module・instance・export・引数・配列は計時前に準備する。invoke・callsのmemory・globalの変更はなく、tableも読み取りだけである。これらの状態復元は不要で、各Invokeは同じ引数から開始する。callsのinstanceとホスト関数はケースごとに生成する。

## 入力と独立した期待値計算

| ケースID | exportと関数型 | 固定入力 | 期待値の根拠 |
| --- | --- | --- | --- |
| `invoke.empty` | `empty: () → ()` | なし | 結果0個 |
| `invoke.single` | `single: (i32) → (i32)` | `ffffffd6`（-42） | 引数をそのまま返す |
| `invoke.multi` | `multi: (i32,i64,f64) → (i32,i64,f64)` | `89abcdef`, `fedcba9876543210`, `8000000000000000`（-0） | 3引数を同じ型・順序・bit列で返す |
| `loop.integer-direct` | `integer_direct: (i32) → (i32)` | 17 | indexが0〜4095の各回に`integer_step: (i32,i32) → (i32)`を直接callし、偶数なら加算、奇数なら減算する。一組で-1のため`17-2048=-2031`、bit列`fffff811` |
| `loop.float-host` | `float_host: (f64) → (f64)` | 1.5 | indexが0〜4095の各回にホスト関数`bench.half: (f64) → (f64)`を呼び、戻り値を加算する。callbackは`input*0.5`を返す。`1.5+4096*4095/4=4193281.5`、bit列`414ffe00c0000000` |
| `loop.indirect` | `indirect: (i32) → (i32)` | 17 | table index`i&1`で`add_one`と`add_three`を交互に呼ぶ。一組で+4のため`17+2048*4=8209`、bit列`00002011` |

上記はWasmの観測値を使わない手計算である。通常TUnitでも閉形式から期待値のbit列を計算する。浮動小数点は全入力・中間値が2進で正確に表せる範囲にあり、近似比較を使わない。callbackは引数以外に依存せず、カウンター・クロージャー等に累積状態を持たせない。

`calls.wat`の唯一のimportは`bench.half`であり、整数・間接callケースのインスタンス化時も同じ型の提供登録が必要。`invoke.wat`はimportを持たない。tableのexport名は`functions`、最小・最大要素数はともに2、要素型は`funcref`。index0は同じinstanceの`add_one`、index1は`add_three`で、ともに`(i32) → (i32)`である。要素をexportされた関数実体と比較できる。

## メモリ操作と走査

`memory.wat`と`scan.wat`はimportを持たず、memoryを`memory`という名前でexportする。入力配列と出力の固定digestは`cases.json`へ保持し、計時外で`WasmMemory.Write`によって準備する。各ケースのinstanceは独立して生成する。

`memory.copy-fill`は3ページ固定のmemoryを使う。0〜65535は`i mod256`のu8固定元領域、65536〜131071は0xa5のfill出力、131072〜196607は固定元と同じcopy出力である。初期状態をこの最終状態へ揃えてから、引数なしの`copy_fill: () → (i32)`を呼ぶ。64KiBのfillと64KiBのcopyを各1回行い、両出力の末尾の和`165+255=420`（`000001a4`）を返す。fillとcopyの出力を分けるため、一方の出力が他方で上書きされない。固定元を含む全3領域のbyte列とdigestを確認し、反復後も同じ状態を保つ。計時内の処理はcopy/fillそのものであり、追加の状態復元処理は含まない。

`scan.scalar`と`scan.simd`は1ページ固定のmemoryを使い、先頭から16384個のu32をlittle-endianで配置する。各要素は`i mod256`である。引数なしの`scalar: () → (i32)`は1要素ずつ、`simd: () → (i32)`はi32x4で4要素ずつ加算し、最後に4laneの和を求める。いずれも読み取る16384要素が仕事量であり、guestループ回数の違いを公開操作数へ換算しない。期待値は独立した閉形式`64*(255*256/2)=2088960`（`001fe000`）である。配列全体も前後に同じdigestと照合する。両ケースを`scan.sum-u32`で対応付ける。

`input_hash`は`CaseInput`を`CaseJson.Options`でコンパクトJSONへ直列化したUTF-8バイト列のSHA-256。関数export名も含むためscalarとSIMDではhashが異なるが、引数と配列生成条件、期待値、仕事量は同じである。`asset_sha256`は固定WABTで変換したバイナリのSHA-256であり、生成ログにsource・profile・ツールとの対応を残す。素材または入力・期待値・操作を変更するときはケース版を更新する。

## C由来の整数行列積

`kernel.c`は32×32のu32行列積を計算する自作freestanding Cで、importを持たない。`matrix: (i32,i32,i32) → ()`の3引数は左入力・右入力・出力のbyte offsetで、それぞれ65536、69632、73728を指定する。各行列は1024個のu32をrow-major、little-endianで格納する。`memory`は2ページ固定で、先頭64KiBをstack用に確保し、入力2領域と出力4096byteを互いに分離する。

`SeededU32`は配列ごとに`state=seed`で開始し、各要素について次の3更新を順番に行った後のstateを格納する。stateは符号なし32bitで、左shiftの上位bitは捨て、右shiftは論理shiftとする。値を小さい範囲へ縮約しない。

```text
state ^= state << 13
state ^= state >> 17
state ^= state << 5
```

左入力のseedは`0x12345678`（305419896）、右入力は`0x9e3779b9`（2654435769）。最初の要素はそれぞれ`0x87985aa5`、`0x510c4619`となる。入力の生成・書き込みは計時外で1回行い、以後は読み取りだけで使う。出力は初期値に依存せず、各Invokeで0から積和を計算して全1024要素を上書きする。前回出力を入力に使わず、別途の状態復元は行わない。1公開Invokeが1操作、1024出力要素と32768回の積和がguestの仕事量である。

Cのunsigned積和はmod2^32で計算する。固定期待値はC#の`BigInteger`で各内積を求め、最後に下位32bitを取る独立参照計算から確定した。全1024要素で内積がuintの上限を超えるため、剰余演算を含む入力である。全4096byteのSHA-256は`bcd94ddfdd7eddddfadf4b84f7c6b7de3ed0c3768607cd53f20227cb6d53fd3c`。通常TUnitでも独立計算と`cases.json`を照合し、Wasmの観測値から期待値を更新しない。

ClangとLLDは21.1.8の明示pathを指定し、コンパイルとlinkを分ける。出力先は新規ディレクトリとする。

```powershell
& $ClangPath --target=wasm32-unknown-unknown -O2 -ffreestanding -nostdlib -fno-vectorize -fno-slp-vectorize -Werror -c tools/WasmSharp.Benchmarks/Assets/sources/kernel.c -o "$OutputDirectory/kernel.o"
& $LldPath --no-entry --export=matrix --export-memory --unresolved-symbols=report-all --stack-first -z stack-size=65536 --initial-memory=131072 --max-memory=131072 "$OutputDirectory/kernel.o" -o "$OutputDirectory/kernel.wasm"
```

`wasm-validate`へは既存`core2-profile.json`のenabled/default_enabled差分だけをfeature引数として渡し、生成物を検査する。公開`InspectImports`でimport0件を確認し、`Decode→Validate→Instantiate→Invoke`で固定digest、反復前後の入力全byte、出力全byteを照合する。出力を意図的に破壊してからのInvokeでも全要素の回復を確認する。生成・検証記録は`artifacts/runtime-performance/task-2.3-20261022T152628Z/`、再生成コマンドと終了値はその`generated/commands.json`に保持する。

通常TUnitは同じ`cases.json`をビルド時にコピーして読み、外部compilerやBDNを起動しない。WAT/Cの変換・Core2検査と生成物の公開API照合は、素材準備時に別途行う。

## 大きいmoduleの準備

`Prepare-Assets.ps1`が`preparation-large.wat`を新規出力先へ生成し、固定WABTで変換・Core2検査する。既存の最小WAT/C smokeも同じ実行で生成・検査する。largeの固定形状は次のとおり。

| 対象 | 固定条件 |
| --- | --- |
| 定義関数 | `f000`〜`f254`の255個とstart1個、計256個。importは数えない。各`fNNN: (i32) → (i32)`は`local.get 0; i32.const NNN; i32.add`で引数へ番号を加算する。`f000`、`f127`、`f254`をexportする。 |
| import | `bench.add_one: (i32) → (i32)`の1件。hostは引数+1をmod2^32で返すstatic callbackとし、累積状態や外部リソース更新を持たない。 |
| memory/data | `memory`は最小・最大とも1ページ。active dataはoffset0から65536byte、各byteは`i mod256`。ホストによる初期入力の書き込みは不要。 |
| table/element | `functions`は`funcref`、最小・最大とも4要素。active elementがindex0〜2へ同じinstanceの`f000`、`f127`、`f254`を設定し、index3はnull。 |
| global/start | immutable i32の`seed`は41。mutable i32の`start_result`の初期値は-1。startは`global.get seed; call add_one; global.set start_result`を実行し、自instanceの値を42へ設定する。 |
| 後検査Invoke | `f254(17)`の結果は`17+254=271`（`0000010f`）。全dataのdigest、table参照、globalも照合する。 |

`decode.large`は事前読込バイト列から毎回新しいmoduleをDecodeし、`decode-validate.large`はさらにValidateまでを1操作とする。`instantiate.large`は検証済みmoduleから毎回新しいinstanceを生成し、import接続・リソース生成・segment初期化・start完了を1操作とする。状態復元は不要で、ケース間で可変リソースを共有しない。Decode後のValidate/Instantiate/Invoke、Decode+Validate後のInstantiate/Invoke、Instantiate後のリソースとInvokeの照合は計時外へ置く。

3ケースの`Input.Arrays`は空とし、data segmentを外部書き込みで置き換えない。`input_hash`は他のケースと同じ`CaseJson.Options`のコンパクトJSONを使う。期待値は上記の手計算と独立して作った65536byteから固定し、Wasmの結果から更新しない。

リポジトリルートで、固定ツールの明示pathと存在しない出力先を指定する。

```powershell
& tools/WasmSharp.Benchmarks/Assets/Prepare-Assets.ps1 `
    -Wat2WasmPath $Wat2WasmPath `
    -WasmValidatePath $WasmValidatePath `
    -ClangPath $ClangPath `
    -WasmLdPath $WasmLdPath `
    -OutputDirectory artifacts/runtime-performance/preparation-large-001
```

`commands.json`に引数・終了値、`sources.json`に生成条件とsource/tool/profile/binaryのhashを保存する。この段階の出力はsmoke2バイナリとlarge1バイナリであり、測定6素材全体のmanifestはタスク2.5で接続する。
