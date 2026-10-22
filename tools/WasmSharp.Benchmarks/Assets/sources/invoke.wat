(module
  (func (export "empty"))

  (func (export "single") (param $value i32) (result i32)
    local.get $value)

  (func (export "multi") (param $a i32) (param $b i64) (param $c f64)
    (result i32 i64 f64)
    local.get $a
    local.get $b
    local.get $c))
