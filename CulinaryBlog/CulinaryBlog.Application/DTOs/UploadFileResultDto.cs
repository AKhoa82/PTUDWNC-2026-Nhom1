namespace CulinaryBlog.Application.DTOs;

public record UploadFileResultDto(
    string Url,
    string FileName,
    string ContentType,
    long SizeBytes
);
