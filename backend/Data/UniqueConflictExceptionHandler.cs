using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

// Endpoints check for duplicates before they insert, but two requests at the same moment (a double click,
// a retry) can both pass the check. The database's primary keys and unique indexes then refuse the second
// insert. That is a conflict, not a server error, so it is answered with 409 like the endpoints' own checks.
public class UniqueConflictExceptionHandler : IExceptionHandler
{
    // SQL Server error numbers for a duplicate key in a unique index and in a primary key or unique constraint.
    private const int DuplicateKeyInUniqueIndex = 2601;
    private const int DuplicateKeyInConstraint = 2627;

    private readonly IProblemDetailsService _problemDetails;

    public UniqueConflictExceptionHandler(IProblemDetailsService problemDetails)
    {
        _problemDetails = problemDetails;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException { InnerException: SqlException sqlException }
            || sqlException.Number is not (DuplicateKeyInUniqueIndex or DuplicateKeyInConstraint))
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status409Conflict,
                Title = "This already exists.",
                Detail = "Another request saved the same thing at the same moment."
            }
        });
    }
}
