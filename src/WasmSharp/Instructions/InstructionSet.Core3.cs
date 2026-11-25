using WasmSharp.Execution;
using static WasmSharp.WasmSpecVersion;

namespace WasmSharp.Instructions;

[Instruction(Core3, 0, 0x08, "throw")]
[Instruction(Core3, 0, 0x0A, "throw_ref")]
[Instruction(Core3, 0, 0x12, "return_call")]
[Instruction(Core3, 0, 0x13, "return_call_indirect")]
[Instruction(
    Core3,
    0,
    0x14,
    "call_ref",
    ImmediateKind.Index,
    StackEffectKind.CallRef,
    ValidationRule.CallRef,
    nameof(Interpreter.CallRef)
)]
[Instruction(Core3, 0, 0x15, "return_call_ref")]
[Instruction(Core3, 0, 0x1F, "try_table")]
[Instruction(
    Core3,
    0,
    0xD3,
    "ref.eq",
    ImmediateKind.None,
    StackEffectKind.RefEq,
    ValidationRule.RefEq,
    nameof(Interpreter.RefEq)
)]
[Instruction(
    Core3,
    0,
    0xD4,
    "ref.as_non_null",
    ImmediateKind.None,
    StackEffectKind.RefAsNonNull,
    ValidationRule.RefAsNonNull,
    nameof(Interpreter.RefAsNonNull)
)]
[Instruction(
    Core3,
    0,
    0xD5,
    "br_on_null",
    ImmediateKind.Index,
    StackEffectKind.BranchOnNull,
    ValidationRule.BranchOnNull,
    nameof(Interpreter.BranchOnNull)
)]
[Instruction(
    Core3,
    0,
    0xD6,
    "br_on_non_null",
    ImmediateKind.Index,
    StackEffectKind.BranchOnNonNull,
    ValidationRule.BranchOnNonNull,
    nameof(Interpreter.BranchOnNonNull)
)]
[Instruction(Core3, 0xFB, 0x00, "struct.new")]
[Instruction(Core3, 0xFB, 0x01, "struct.new_default")]
[Instruction(Core3, 0xFB, 0x02, "struct.get")]
[Instruction(Core3, 0xFB, 0x03, "struct.get_s")]
[Instruction(Core3, 0xFB, 0x04, "struct.get_u")]
[Instruction(Core3, 0xFB, 0x05, "struct.set")]
[Instruction(Core3, 0xFB, 0x06, "array.new")]
[Instruction(Core3, 0xFB, 0x07, "array.new_default")]
[Instruction(Core3, 0xFB, 0x08, "array.new_fixed")]
[Instruction(Core3, 0xFB, 0x09, "array.new_data")]
[Instruction(Core3, 0xFB, 0x0A, "array.new_elem")]
[Instruction(Core3, 0xFB, 0x0B, "array.get")]
[Instruction(Core3, 0xFB, 0x0C, "array.get_s")]
[Instruction(Core3, 0xFB, 0x0D, "array.get_u")]
[Instruction(Core3, 0xFB, 0x0E, "array.set")]
[Instruction(Core3, 0xFB, 0x0F, "array.len")]
[Instruction(Core3, 0xFB, 0x10, "array.fill")]
[Instruction(Core3, 0xFB, 0x11, "array.copy")]
[Instruction(Core3, 0xFB, 0x12, "array.init_data")]
[Instruction(Core3, 0xFB, 0x13, "array.init_elem")]
[Instruction(
    Core3,
    0xFB,
    0x14,
    "ref.test",
    ImmediateKind.CastType,
    StackEffectKind.RefTest,
    ValidationRule.RefTest,
    nameof(Interpreter.RefTest)
)]
[Instruction(
    Core3,
    0xFB,
    0x15,
    "ref.test",
    ImmediateKind.NullableCastType,
    StackEffectKind.RefTest,
    ValidationRule.RefTest,
    nameof(Interpreter.RefTest)
)]
[Instruction(
    Core3,
    0xFB,
    0x16,
    "ref.cast",
    ImmediateKind.CastType,
    StackEffectKind.RefCast,
    ValidationRule.RefCast,
    nameof(Interpreter.RefCast)
)]
[Instruction(
    Core3,
    0xFB,
    0x17,
    "ref.cast",
    ImmediateKind.NullableCastType,
    StackEffectKind.RefCast,
    ValidationRule.RefCast,
    nameof(Interpreter.RefCast)
)]
[Instruction(
    Core3,
    0xFB,
    0x18,
    "br_on_cast",
    ImmediateKind.CastBranch,
    StackEffectKind.BranchOnCast,
    ValidationRule.BranchOnCast,
    nameof(Interpreter.BranchOnCast)
)]
[Instruction(
    Core3,
    0xFB,
    0x19,
    "br_on_cast_fail",
    ImmediateKind.CastBranch,
    StackEffectKind.BranchOnCastFail,
    ValidationRule.BranchOnCastFail,
    nameof(Interpreter.BranchOnCastFail)
)]
[Instruction(
    Core3,
    0xFB,
    0x1A,
    "any.convert_extern",
    ImmediateKind.None,
    StackEffectKind.AnyConvertExtern,
    ValidationRule.AnyConvertExtern,
    nameof(Interpreter.AnyConvertExtern)
)]
[Instruction(
    Core3,
    0xFB,
    0x1B,
    "extern.convert_any",
    ImmediateKind.None,
    StackEffectKind.ExternConvertAny,
    ValidationRule.ExternConvertAny,
    nameof(Interpreter.ExternConvertAny)
)]
[Instruction(
    Core3,
    0xFB,
    0x1C,
    "ref.i31",
    ImmediateKind.None,
    StackEffectKind.RefI31,
    ValidationRule.RefI31,
    nameof(Interpreter.RefI31)
)]
[Instruction(
    Core3,
    0xFB,
    0x1D,
    "i31.get_s",
    ImmediateKind.None,
    StackEffectKind.I31Get,
    ValidationRule.I31Get,
    nameof(Interpreter.I31GetS)
)]
[Instruction(
    Core3,
    0xFB,
    0x1E,
    "i31.get_u",
    ImmediateKind.None,
    StackEffectKind.I31Get,
    ValidationRule.I31Get,
    nameof(Interpreter.I31GetU)
)]
[Instruction(Core3, 0xFD, 0x100, "i8x16.relaxed_swizzle")]
[Instruction(Core3, 0xFD, 0x101, "i32x4.relaxed_trunc_f32x4_s")]
[Instruction(Core3, 0xFD, 0x102, "i32x4.relaxed_trunc_f32x4_u")]
[Instruction(Core3, 0xFD, 0x103, "i32x4.relaxed_trunc_f64x2_s")]
[Instruction(Core3, 0xFD, 0x104, "i32x4.relaxed_trunc_f64x2_u")]
[Instruction(Core3, 0xFD, 0x105, "f32x4.relaxed_madd")]
[Instruction(Core3, 0xFD, 0x106, "f32x4.relaxed_nmadd")]
[Instruction(Core3, 0xFD, 0x107, "f64x2.relaxed_madd")]
[Instruction(Core3, 0xFD, 0x108, "f64x2.relaxed_nmadd")]
[Instruction(Core3, 0xFD, 0x109, "i8x16.relaxed_laneselect")]
[Instruction(Core3, 0xFD, 0x10A, "i16x8.relaxed_laneselect")]
[Instruction(Core3, 0xFD, 0x10B, "i32x4.relaxed_laneselect")]
[Instruction(Core3, 0xFD, 0x10C, "i64x2.relaxed_laneselect")]
[Instruction(Core3, 0xFD, 0x10D, "f32x4.relaxed_min")]
[Instruction(Core3, 0xFD, 0x10E, "f32x4.relaxed_max")]
[Instruction(Core3, 0xFD, 0x10F, "f64x2.relaxed_min")]
[Instruction(Core3, 0xFD, 0x110, "f64x2.relaxed_max")]
[Instruction(Core3, 0xFD, 0x111, "i16x8.relaxed_q15mulr_s")]
[Instruction(Core3, 0xFD, 0x112, "i16x8.relaxed_dot_i8x16_i7x16_s")]
[Instruction(Core3, 0xFD, 0x113, "i32x4.relaxed_dot_i8x16_i7x16_add_s")]
internal static partial class InstructionSet;
