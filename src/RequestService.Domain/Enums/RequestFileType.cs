namespace RequestService.Domain.Enums;

/// <summary>High-level category of an attached file. The real file lives in the files service.</summary>
public enum RequestFileType
{
    Audio,
    Image,
    Video,
    Other
}
