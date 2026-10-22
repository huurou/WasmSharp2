(module
  (import "bench" "add_one" (func $add_one (param i32) (result i32)))
  (import "bench" "half" (func $half (param f64) (result f64)))
  (export "add_one" (func $add_one))
  (export "half" (func $half))
  (memory (export "memory") 1)
  (table (export "functions") 3 4 funcref)
  (elem (i32.const 0) $probe $add_one)
  (global $seed (export "seed") i32 (i32.const 41))
  (global $result (export "start_result") (mut i32) (i32.const -1))
  (func $start global.get $seed call $add_one global.set $result)
  (start $start)
  (data (i32.const 0) "\00\01\02\03\04\05\06\07")
  (func $probe (export "probe") (param i32) (result i32) local.get 0)
  (func (export "repair") (param i32) (result i32)
    i32.const 42 global.set $result local.get 0)
  (func (export "copy_fill") (result i32)
    i32.const 16 i32.const 165 i32.const 8 memory.fill
    i32.const 24 i32.const 0 i32.const 8 memory.copy
    i32.const 420)
  (func (export "matrix")
    i32.const 16 i32.const 16909060 i32.store
    i32.const 20 i32.const -1 i32.store)
  (func (export "trap") unreachable))
