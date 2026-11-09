namespace CseReviewer;
public class ServiceResult {
    public bool IsSuccess { get; init; } = true;
    public int Status { get; init; } = 200;
    public string Error { get; init; } = "";
    public string Code { get; init; } = "";
    public static ServiceResult Ok() => new();
    public static ServiceResult Fail(string error, int status = 400, string code = "") => new() { IsSuccess = false, Status = status, Error = error, Code = code };
}
public class ServiceResult<T> : ServiceResult {
    public T? Value { get; init; }
    public static ServiceResult<T> Ok(T value) => new() { Value = value };
    public static new ServiceResult<T> Fail(string error, int status = 400, string code = "") => new() { IsSuccess = false, Status = status, Error = error, Code = code };
}
public static class ServiceResultExtensions {
    public static IResult ToHttp(this ServiceResult result) => result.IsSuccess ? Results.Ok() : ToHttpError(result);
    public static IResult ToHttp<T>(this ServiceResult<T> result) => result.IsSuccess ? Results.Ok(result.Value) : ToHttpError(result);
    public static IResult ToHttp<T, TBody>(this ServiceResult<T> result, Func<T, TBody> shapeBody) => result.IsSuccess ? Results.Ok(shapeBody(result.Value!)) : ToHttpError(result);
    private static IResult ToHttpError(ServiceResult result) {
        if (result.Error.Length == 0) return Results.StatusCode(result.Status);
        object body = result.Code.Length == 0 ? new { error = result.Error } : new { error = result.Error, code = result.Code };
        return Results.Json(body, statusCode: result.Status);
    }
}
