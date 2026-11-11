namespace BH_AEP.Application.Common;

public class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }

    public AppException(string message, Exception inner, int statusCode = 400) : base(message, inner)
    {
        StatusCode = statusCode;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404)
    {
    }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "غير مصرح لك بهذه العملية.") : base(message, 403)
    {
    }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "جلسة غير صالحة أو منتهية.") : base(message, 401)
    {
    }
}

public class ValidationException2 : AppException
{
    public ValidationException2(string message) : base(message, 422)
    {
    }
}
