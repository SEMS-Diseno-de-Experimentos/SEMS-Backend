namespace Sems.Api.Shared.Configuration;

/// <summary>
/// Carga un archivo <c>.env</c> a variables de entorno del proceso.
///
/// <para>ASP.NET Core no lee <c>.env</c> por su cuenta, a diferencia de Spring
/// Boot, que lo importaba con <c>spring.config.import</c>. Sin esto el archivo
/// <c>.env.example</c> no serviria de nada y habria que exportar cada variable a
/// mano antes de arrancar.</para>
///
/// <para><b>Solo es para desarrollo local.</b> En el hosting las variables se
/// definen en el panel del proveedor y el archivo no existe, por eso su ausencia
/// no es un error.</para>
/// </summary>
public static class DotEnv
{
    /// <summary>
    /// Lee el archivo indicado si existe. Las variables que ya esten definidas
    /// en el entorno <b>no se pisan</b>: lo que configure el hosting siempre
    /// gana sobre un archivo que alguien se dejo olvidado en su maquina.
    /// </summary>
    public static void Load(string path = ".env")
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim();

            // Las comillas son del formato del archivo, no parte del valor.
            if (value.Length >= 2
                && ((value[0] == '"' && value[^1] == '"')
                    || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
