namespace ModernMediator.Sample.WebApi.Advanced;

public static class ResultExtensions
{
    public static Microsoft.AspNetCore.Http.IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.Code switch
            {
                "NOT_FOUND" => Results.NotFound(result.Error.Message),
                "ALREADY_EXISTS" => Results.Conflict(result.Error.Message),
                "INVALID_SPECIES" => Results.BadRequest(result.Error.Message),
                _ => Results.Problem(result.Error.Message)
            };
}
