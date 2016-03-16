using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using RpcReceiver.Entities;

namespace RpcReceiver.Mapping
{
    class GroupMapper : EntityTypeConfiguration<Group>
    {
        public GroupMapper()
        {
            this.ToTable("Group");
            this.HasKey(c => c.Id);
            this.Property(c => c.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            this.Property(c => c.Id).IsRequired();

            this.Property(c => c.GroupName).IsRequired();
            this.Property(c => c.GroupName).HasMaxLength(1000);

            this.HasRequired(c => c.Applications).WithMany().Map(s => s.MapKey("ApplicationId"));
        }
    }
}
