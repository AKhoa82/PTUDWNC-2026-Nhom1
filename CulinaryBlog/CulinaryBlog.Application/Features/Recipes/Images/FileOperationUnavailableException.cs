namespace CulinaryBlog.Application.Features.Recipes.Images;

public sealed class FileOperationUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
