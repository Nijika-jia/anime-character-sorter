namespace AnimeSorterWin.Models;

/// <summary>
/// 输出目录结构的归类方式。
/// </summary>
public enum OutputOrganizationMode
{
    /// <summary>
    /// 输出根目录/作品名称/角色名称（与当前默认一致）
    /// </summary>
    SeriesThenCharacter,

    /// <summary>
    /// 输出根目录/作品名称/
    /// </summary>
    SeriesOnly,

    /// <summary>
    /// 输出根目录/角色名称/
    /// </summary>
    CharacterOnly,

    /// <summary>
    /// 输出根目录/角色名称/作品名称
    /// </summary>
    CharacterThenSeries
}

