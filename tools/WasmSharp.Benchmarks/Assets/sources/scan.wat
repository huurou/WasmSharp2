(module
  (memory (export "memory") 1 1)

  ;; 両exportは計時前に用意した同じ16384個のu32を読み取る。
  (func (export "scalar") (result i32)
    (local $offset i32)
    (local $sum i32)
    loop $next
      local.get $sum
      local.get $offset
      i32.load
      i32.add
      local.set $sum
      local.get $offset
      i32.const 4
      i32.add
      local.tee $offset
      i32.const 65536
      i32.lt_u
      br_if $next
    end
    local.get $sum)

  (func (export "simd") (result i32)
    (local $offset i32)
    (local $sums v128)
    loop $next
      local.get $sums
      local.get $offset
      v128.load
      i32x4.add
      local.set $sums
      local.get $offset
      i32.const 16
      i32.add
      local.tee $offset
      i32.const 65536
      i32.lt_u
      br_if $next
    end

    ;; 各laneの部分和を加算してscalarと同じ1個のi32を返す。
    local.get $sums
    i32x4.extract_lane 0
    local.get $sums
    i32x4.extract_lane 1
    i32.add
    local.get $sums
    i32x4.extract_lane 2
    i32.add
    local.get $sums
    i32x4.extract_lane 3
    i32.add))
