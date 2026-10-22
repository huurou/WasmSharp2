(module
  (memory (export "memory") 3 3)

  ;; 元領域、fill出力、copy出力を各64KiBに分け、計時前に最終状態へ揃える。
  (func (export "copy_fill") (result i32)
    i32.const 65536
    i32.const 165
    i32.const 65536
    memory.fill
    i32.const 131072
    i32.const 0
    i32.const 65536
    memory.copy

    ;; 両出力の末尾を結果に使い、出力全体の照合は計時外で行う。
    i32.const 131071
    i32.load8_u
    i32.const 196607
    i32.load8_u
    i32.add))
