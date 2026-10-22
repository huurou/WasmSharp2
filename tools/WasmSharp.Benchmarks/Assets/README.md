# 素材とケースの固定条件

タスク2.1・2.2で追加する4素材・9ケース。固定値は`cases.json`の既存`BenchmarkCase`形式で保持する。6素材・15ケースへの統合とmanifest生成はタスク2.5、ケース読込・公開APIの検査器はタスク3.1・3.2で接続する。

全ケースは1回の公開Invokeを1操作とする。loopの4096回はguestの仕事量であり、操作数ではない。module・instance・export・引数・配列は計時前に準備する。invoke・callsのmemory・globalの変更はなく、tableも読み取りだけである。これらの状態復元は不要で、各Invokeは同じ引数から開始する。callsのinstanceとホスト関数はケースごとに生成する。

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

通常TUnitは同じ`cases.json`をビルド時にコピーして読み、外部compilerやBDNを起動しない。WATの変換・Core2検査と生成物の公開API照合は、素材準備時に別途行う。
