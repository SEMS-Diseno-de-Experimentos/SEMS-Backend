namespace Sems.Api.Modules.Iam.Domain.Services;

/// <summary>
/// Comprueba un token de identidad de Google y devuelve lo que afirma.
/// </summary>
/// <remarks>
/// <para>Es un puerto porque quien decide si el token vale es Google, no
/// nosotros: el caso de uso solo necesita las afirmaciones ya verificadas. Y
/// porque sin el, probar el alta con Google exigiria una cuenta real y salir a
/// internet en cada ejecucion.</para>
/// </remarks>
public interface IGoogleTokenVerifier
{
    /// <summary>
    /// Lo que Google afirma del token.
    /// </summary>
    /// <param name="Audience">
    /// Para que aplicacion se emitio. Si no es la nuestra, el token es de otro
    /// sitio y no sirve aqui por muy valido que sea.
    /// </param>
    /// <param name="Email">
    /// El correo de la cuenta de Google. Es la identidad con la que se busca al
    /// usuario, y con la que se le crea si es la primera vez que entra.
    /// </param>
    /// <param name="EmailVerified">
    /// Si Google comprobo que ese correo es de quien lo presenta. Cuando es
    /// falso se rechaza la entrada: un correo sin verificar permitiria entrar
    /// con la direccion de otra persona.
    /// </param>
    /// <param name="Name">
    /// El nombre para mostrar, tal y como lo da Google. Es opcional porque no
    /// todas las cuentas lo publican; entonces llega null y la cuenta se crea
    /// sin nombre.
    /// </param>
    public sealed record GoogleIdentity(string Audience, string Email, bool EmailVerified,
        string? Name);

    /// <summary>Las afirmaciones del token, o null si Google no lo reconoce.</summary>
    Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken ct = default);
}
