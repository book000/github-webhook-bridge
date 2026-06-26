// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// テストプロジェクト固有の命名規則・解析警告を抑制する。
using System.Diagnostics.CodeAnalysis;

// テストメソッドのアンダースコア区切り命名規則（Method_Scenario_Expected）に対する CA1707 を抑制する。
[assembly: SuppressMessage(
    "Naming",
    "CA1707:IdentifiersShouldNotContainUnderscores",
    Justification = "Test methods use underscore-separated naming convention (Method_Scenario_Expected).",
    Scope = "module")]

// xUnit の [Fact]/[Theory] メソッドは 'Async' サフィックスを必須としない規約に従う。
[assembly: SuppressMessage(
    "Naming",
    "IDE1006:Naming Styles",
    Justification = "xUnit test methods marked with [Fact]/[Theory] do not require the 'Async' suffix.",
    Scope = "module")]

// 署名検証テストで HMAC-SHA256 の出力を小文字 HEX に変換する処理は意図的。
// SignatureValidator が小文字で比較するため ToLowerInvariant の使用が正しい。
[assembly: SuppressMessage(
    "Globalization",
    "CA1308:Normalize strings to uppercase",
    Justification = "HMAC-SHA256 hex strings are intentionally lowercased to match the SignatureValidator's comparison format.",
    Scope = "module")]
