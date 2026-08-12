namespace Kura.Api.Filters;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Comparação constant-time de API key, usada por <see cref="ApiKeyAuthFilter"/> e
/// <see cref="LunaApiKeyAuthFilter"/> (TASK-86, item 6). Helper estático, não serviço
/// via DI — os dois filtros já são desenhados como "sibling, não generalizado" de
/// propósito (ver comentário no topo de <see cref="LunaApiKeyAuthFilter"/>), e este
/// helper não muda essa decisão de design.
///
/// Antes desta task, os dois filtros comparavam a API key com o operador <c>!=</c>
/// padrão (<c>StringValues != string</c>), que por baixo é <c>String.Equals</c>
/// ordinal — compara caractere a caractere e sai no primeiro byte diferente
/// (early-exit), o padrão clássico vulnerável a timing attack: o tempo de resposta
/// varia de forma mensurável com quantos caracteres do início batem. O lado Python
/// (Luna) já usa <c>secrets.compare_digest</c>
/// (kura-luna-ai/luna/src/web/dependencies.py:44), constant-time por construção — o
/// .NET estava fora do padrão nos 2 filtros equivalentes.
/// </summary>
public static class ApiKeyComparer
{
    /// <summary>
    /// Compara <paramref name="provided"/> (valor do header) com
    /// <paramref name="configured"/> (valor de config) usando
    /// <see cref="CryptographicOperations.FixedTimeEquals"/>.
    ///
    /// LIMITAÇÃO DECLARADA: é constant-time quanto ao CONTEÚDO dos bytes, NÃO quanto
    /// ao TAMANHO — <c>FixedTimeEquals</c> retorna <see langword="false"/>
    /// imediatamente se os dois spans tiverem tamanhos diferentes, antes de comparar
    /// nenhum byte. Isso é aceitável neste caso: o tamanho de uma API key não é
    /// segredo por caractere, é um detalhe de configuração — diferente do conteúdo,
    /// que é o segredo de fato. Não escondendo essa limitação: se algum dia o
    /// tamanho da chave em si precisar ser tratado como sigiloso, este helper não
    /// serve sem um passo extra de padding.
    /// </summary>
    public static bool IsMatch(StringValues provided, string configured)
    {
        var providedValue = provided.ToString();

        var providedBytes = Encoding.UTF8.GetBytes(providedValue);
        var configuredBytes = Encoding.UTF8.GetBytes(configured);

        return CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
