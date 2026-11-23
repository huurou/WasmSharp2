using System.Text;

namespace WasmSharp;

/// <summary>
/// import宣言の名前、種類と解決済みの要求型
/// </summary>
public abstract record WasmImportInfo
{
    /// <summary>
    /// 要求する提供元のmodule名
    /// </summary>
    public string ModuleName { get; }

    /// <summary>
    /// 要求する外部要素の名前
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 外部要素の種類
    /// </summary>
    public WasmExternalKind Kind => ExternalKind;

    /// <summary>
    /// 派生型ごとの外部要素の種類を取得する。実装を同一アセンブリへ限定し、外部からの派生を防ぐ
    /// </summary>
    private protected abstract WasmExternalKind ExternalKind { get; }

    /// <summary>
    /// import宣言の共通の名前情報を初期化する
    /// </summary>
    /// <param name="moduleName">要求する提供元のmodule名</param>
    /// <param name="name">要求する外部要素の名前</param>
    private WasmImportInfo(string moduleName, string name)
    {
        ModuleName = moduleName;
        Name = name;
    }

    /// <summary>
    /// 関数importの要求型
    /// </summary>
    /// <param name="ModuleName">要求する提供元のmodule名</param>
    /// <param name="Name">要求する外部要素の名前</param>
    /// <param name="Type">解決済みの関数型</param>
    public sealed record Function(string ModuleName, string Name, WasmFunctionType Type) : WasmImportInfo(ModuleName, Name)
    {
        /// <inheritdoc />
        private protected override WasmExternalKind ExternalKind => WasmExternalKind.Function;
    }

    /// <summary>
    /// global importの要求型
    /// </summary>
    /// <param name="ModuleName">要求する提供元のmodule名</param>
    /// <param name="Name">要求する外部要素の名前</param>
    /// <param name="Type">値型と可変性</param>
    public sealed record Global(string ModuleName, string Name, WasmGlobalType Type) : WasmImportInfo(ModuleName, Name)
    {
        /// <inheritdoc />
        private protected override WasmExternalKind ExternalKind => WasmExternalKind.Global;
    }

    /// <summary>
    /// memory importのアドレス型と要求するページ数の上下限
    /// </summary>
    public sealed record Memory : WasmImportInfo
    {
        /// <summary>
        /// 宣言のアドレス型と64ビット精度の最小・最大ページ数
        /// </summary>
        public WasmMemoryType Type { get; private init; }

        /// <summary>
        /// 32ビットメモリが要求する最小・最大ページ数
        /// </summary>
        /// <exception cref="InvalidOperationException">64ビットメモリで取得または設定した場合、または取得するページ数が32ビットで表せない場合</exception>
        /// <exception cref="ArgumentNullException">nullを設定した場合</exception>
        public WasmLimits Limits
        {
            get => Type.Get32BitLimits();
            init
            {
                ArgumentNullException.ThrowIfNull(value);
                if (Type.AddressType != WasmValueKind.I32)
                {
                    throw new InvalidOperationException("64ビットメモリのページ数はLimitsで設定できません。");
                }
                Type = new WasmMemoryType(value);
            }
        }

        /// <inheritdoc />
        private protected override WasmExternalKind ExternalKind => WasmExternalKind.Memory;

        /// <summary>
        /// 32ビットメモリのimport情報を生成する
        /// </summary>
        /// <param name="ModuleName">要求する提供元のmodule名</param>
        /// <param name="Name">要求する外部要素の名前</param>
        /// <param name="Limits">宣言された最小値と任意の最大値</param>
        public Memory(string ModuleName, string Name, WasmLimits Limits)
            : base(ModuleName, Name)
        {
            ArgumentNullException.ThrowIfNull(Limits);
            Type = new WasmMemoryType(Limits);
        }

        internal Memory(WasmMemoryType type, string moduleName, string name)
            : base(moduleName, name)
        {
            Type = type;
        }

        /// <summary>
        /// 32ビットメモリのimport情報を名前とlimitsに分解する
        /// </summary>
        /// <exception cref="InvalidOperationException">アドレス型またはページ数を32ビットメモリのlimitsで表せない場合</exception>
        public void Deconstruct(out string ModuleName, out string Name, out WasmLimits Limits)
        {
            ModuleName = this.ModuleName;
            Name = this.Name;
            Limits = this.Limits;
        }

        /// <inheritdoc />
        protected override bool PrintMembers(StringBuilder builder)
        {
            base.PrintMembers(builder);
            builder.Append(Type.Has32BitLimits ? $", Limits = {Limits}" : $", Type = {Type}");
            return true;
        }
    }

    /// <summary>
    /// テーブルのimportが要求する参照型、アドレス型と要素数の上下限
    /// </summary>
    public sealed record Table : WasmImportInfo
    {
        /// <summary>
        /// 宣言の要素型、アドレス型と64ビット精度の要素数の上下限
        /// </summary>
        public WasmTableType Type { get; private init; }

        /// <summary>
        /// テーブルの要素の参照型（<see cref="WasmValueKind.FuncRef"/>または<see cref="WasmValueKind.ExternRef"/>）
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">要素型として使用できない値を設定した場合</exception>
        public WasmValueKind ElementType
        {
            get => Type.ElementType;
            init => Type = new WasmTableType(value, Type.AddressType, Type.MinimumElements, Type.MaximumElements);
        }

        /// <summary>
        /// 32ビットテーブルが要求する要素数の上下限
        /// </summary>
        /// <exception cref="InvalidOperationException">64ビットテーブルで取得または設定した場合、または読み取る要素数が32ビットで表せない場合</exception>
        /// <exception cref="ArgumentNullException">nullを設定した場合</exception>
        public WasmLimits Limits
        {
            get => Type.Get32BitLimits();
            init
            {
                ArgumentNullException.ThrowIfNull(value);
                if (Type.AddressType != WasmValueKind.I32)
                {
                    throw new InvalidOperationException("64ビットテーブルの要素数はLimitsで設定できません。");
                }
                Type = new WasmTableType(ElementType, value);
            }
        }

        /// <inheritdoc />
        private protected override WasmExternalKind ExternalKind => WasmExternalKind.Table;

        /// <summary>
        /// 32ビットテーブルのimport情報を生成する
        /// </summary>
        /// <param name="ModuleName">要求する提供元のmodule名</param>
        /// <param name="Name">要求する外部要素の名前</param>
        /// <param name="ElementType">要素の参照型（<see cref="WasmValueKind.FuncRef"/>または<see cref="WasmValueKind.ExternRef"/>）</param>
        /// <param name="Limits">宣言された最小値と任意の最大値</param>
        /// <exception cref="ArgumentNullException"><paramref name="Limits"/>がnullの場合</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="ElementType"/>が対応する参照型以外の場合</exception>
        public Table(string ModuleName, string Name, WasmValueKind ElementType, WasmLimits Limits)
            : base(ModuleName, Name)
        {
            ArgumentNullException.ThrowIfNull(Limits);
            Type = new WasmTableType(ElementType, Limits);
        }

        internal Table(WasmTableType type, string moduleName, string name)
            : base(moduleName, name)
        {
            Type = type;
        }

        /// <summary>
        /// 32ビットテーブルのimport情報を名前・要素型・上下限に分解する
        /// </summary>
        /// <exception cref="InvalidOperationException">アドレス型または要素数を32ビットテーブルのlimitsで表せない場合</exception>
        public void Deconstruct(out string ModuleName, out string Name, out WasmValueKind ElementType, out WasmLimits Limits)
        {
            ModuleName = this.ModuleName;
            Name = this.Name;
            ElementType = this.ElementType;
            Limits = this.Limits;
        }

        /// <inheritdoc />
        protected override bool PrintMembers(StringBuilder builder)
        {
            base.PrintMembers(builder);
            builder.Append(Type.Has32BitLimits ? $", ElementType = {ElementType}, Limits = {Limits}" : $", Type = {Type}");
            return true;
        }
    }
}
