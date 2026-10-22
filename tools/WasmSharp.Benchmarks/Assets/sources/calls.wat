(module
  (type $unary_i32 (func (param i32) (result i32)))
  ;; callbackは入力の半分を返し、呼び出し間に状態を持たない。
  (import "bench" "half" (func $half (param f64) (result f64)))

  (table (export "functions") 2 2 funcref)
  (elem (i32.const 0) $add_one $add_three)

  (func $add_one (export "add_one") (type $unary_i32)
    local.get 0
    i32.const 1
    i32.add)

  (func $add_three (export "add_three") (type $unary_i32)
    local.get 0
    i32.const 3
    i32.add)

  (func $integer_step (param $sum i32) (param $index i32) (result i32)
    local.get $index
    i32.const 1
    i32.and
    if (result i32)
      local.get $sum
      local.get $index
      i32.sub
    else
      local.get $sum
      local.get $index
      i32.add
    end)

  (func (export "integer_direct") (param $sum i32) (result i32)
    (local $index i32)
    loop $next
      local.get $sum
      local.get $index
      call $integer_step
      local.set $sum
      local.get $index
      i32.const 1
      i32.add
      local.tee $index
      i32.const 4096
      i32.lt_u
      br_if $next
    end
    local.get $sum)

  (func (export "float_host") (param $sum f64) (result f64)
    (local $index i32)
    loop $next
      local.get $sum
      local.get $index
      f64.convert_i32_u
      call $half
      f64.add
      local.set $sum
      local.get $index
      i32.const 1
      i32.add
      local.tee $index
      i32.const 4096
      i32.lt_u
      br_if $next
    end
    local.get $sum)

  (func (export "indirect") (param $sum i32) (result i32)
    (local $index i32)
    loop $next
      local.get $sum
      local.get $index
      i32.const 1
      i32.and
      call_indirect (type $unary_i32)
      local.set $sum
      local.get $index
      i32.const 1
      i32.add
      local.tee $index
      i32.const 4096
      i32.lt_u
      br_if $next
    end
    local.get $sum))
