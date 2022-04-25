﻿namespace GradeCenter.Server.Data
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using GradeCenter.Server.Data.Common.Models;
    using GradeCenter.Server.Data.Models;

    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;

    public class GradeCenterDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        private static readonly MethodInfo SetIsDeletedQueryFilterMethod =
            typeof(GradeCenterDbContext).GetMethod(
                nameof(SetIsDeletedQueryFilter),
                BindingFlags.NonPublic | BindingFlags.Static);

        public GradeCenterDbContext(DbContextOptions<GradeCenterDbContext> options)
            : base(options)
        {
        }

        public DbSet<School> Schools { get; set; }

        public DbSet<Class> Classes { get; set; }

        public DbSet<UserRelation> UsersRelations { get; set; }

        public DbSet<Curriculum> Curriculums { get; set; }

        public DbSet<Subject> Subjects { get; set; }

        public DbSet<CurriculumSubject> CurriculumsSubjects { get; set; }

        public DbSet<UserGrade> UsersGrades { get; set; }

        public DbSet<UserSubject> UsersSubjects { get; set; }

        public DbSet<UserPresence> UsersPresences { get; set; }

        public override int SaveChanges() => this.SaveChanges(true);

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            this.ApplyAuditInfoRules();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            this.SaveChangesAsync(true, cancellationToken);

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            this.ApplyAuditInfoRules();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Needed for Identity models configuration
            base.OnModelCreating(builder);

            this.ConfigureUserIdentityRelations(builder);

            EntityIndexesConfiguration.Configure(builder);

            var entityTypes = builder.Model.GetEntityTypes().ToList();

            // Set global query filter for not deleted entities only
            var deletableEntityTypes = entityTypes
                .Where(et => et.ClrType != null && typeof(IDeletableEntity).IsAssignableFrom(et.ClrType));
            foreach (var deletableEntityType in deletableEntityTypes)
            {
                var method = SetIsDeletedQueryFilterMethod.MakeGenericMethod(deletableEntityType.ClrType);
                method.Invoke(null, new object[] { builder });
            }
        }

        private static void SetIsDeletedQueryFilter<T>(ModelBuilder builder)
            where T : class, IDeletableEntity
        {
            builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
        }

        // Applies configurations
        private void ConfigureUserIdentityRelations(ModelBuilder builder)
             => builder.ApplyConfigurationsFromAssembly(this.GetType().Assembly);

        private void ApplyAuditInfoRules()
        {
            var changedEntries = this.ChangeTracker
                .Entries()
                .Where(e =>
                    ((e.Entity is IDeletableEntity && e.State == EntityState.Deleted)
                      || (e.Entity is IAuditInfo && (e.State is EntityState.Added or EntityState.Modified))));

            foreach (var entry in changedEntries)
            {
                if (entry.State == EntityState.Deleted && entry.Entity is IDeletableEntity deletableEntity)
                {
                    entry.State = EntityState.Unchanged;
                    deletableEntity.DeletedOn = DateTime.UtcNow;
                    deletableEntity.IsDeleted = true;
                }
                else if (entry.Entity is IAuditInfo auditInfoEntity)
                {
                    if (entry.State == EntityState.Added && auditInfoEntity.CreatedOn == default)
                    {
                        auditInfoEntity.CreatedOn = DateTime.UtcNow;
                    }
                    else
                    {
                        auditInfoEntity.ModifiedOn = DateTime.UtcNow;
                    }
                }
            }
        }
    }
}
