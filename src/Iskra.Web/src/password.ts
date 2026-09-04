const SPECIALS = new Set("~!@#$%^&*()\\|/,.<>".split(""));

export function passwordErrorKey(password: string): string | null {
  if (!password) return "pass.empty";
  if (password.length < 8) return "pass.too_short";
  let hasUpper = false;
  let hasLetter = false;
  let hasDigit = false;
  for (const c of password) {
    if (c >= "A" && c <= "Z") {
      hasUpper = true;
      hasLetter = true;
    } else if (c >= "a" && c <= "z") {
      hasLetter = true;
    } else if (c >= "0" && c <= "9") {
      hasDigit = true;
    } else if (!SPECIALS.has(c)) {
      return "pass.invalid_char";
    }
  }
  if (!hasUpper) return "pass.need_upper";
  if (!hasLetter) return "pass.need_letter";
  if (!hasDigit) return "pass.need_digit";
  return null;
}
