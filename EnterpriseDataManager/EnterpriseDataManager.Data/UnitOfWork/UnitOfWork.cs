namespace EnterpriseDataManager.Data.UnitOfWork;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public class UnitOfWork : IUnitOfWork
{
    private readonly EnterpriseDataManagerDbContext _context;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    private IArchivePlanRepository? _archivePlans;
    private IArchiveJobRepository? _archiveJobs;
    private IRepository<ArchiveItem>? _archiveItems;
    private IRecoveryJobRepository? _recoveryJobs;
    private IRetentionPolicyRepository? _retentionPolicies;
    private IStorageProviderRepository? _storageProviders;
    private IAuditRecordRepository? _auditRecords;

    public UnitOfWork(EnterpriseDataManagerDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IArchivePlanRepository ArchivePlans =>
        _archivePlans ??= new ArchivePlanRepository(_context);

    public IArchiveJobRepository ArchiveJobs =>
        _archiveJobs ??= new ArchiveJobRepository(_context);

    public IRepository<ArchiveItem> ArchiveItems =>
        _archiveItems ??= new GenericRepository<ArchiveItem>(_context);

    public IRecoveryJobRepository RecoveryJobs =>
        _recoveryJobs ??= new RecoveryJobRepository(_context);

    public IRetentionPolicyRepository RetentionPolicies =>
        _retentionPolicies ??= new RetentionPolicyRepository(_context);

    public IStorageProviderRepository StorageProviders =>
        _storageProviders ??= new StorageProviderRepository(_context);

    public IAuditRecordRepository AuditRecords =>
        _auditRecords ??= new AuditRecordRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync(string? userId, CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(userId, cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No transaction is in progress.");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _currentTransaction?.Dispose();
                _context.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
