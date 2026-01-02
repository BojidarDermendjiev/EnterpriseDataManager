namespace EnterpriseDataManager.Application.Common.Interfaces;

using MediatR;

public interface IQuery<TResponse> : IRequest<TResponse>
{
}

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
}
