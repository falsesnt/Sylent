namespace SylentLoader.Services.Auth;

public readonly record struct AuthResult(bool Ok, string Message, bool Transient = false);
