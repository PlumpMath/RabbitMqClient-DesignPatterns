using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RpcReceiver.Entities;

namespace RpcReceiver.Mapping
{
    class ApplicationMapping: EntityTypeConfiguration<Application>
    {
        public ApplicationMapping()
        {
            this.ToTable("Application");
            this.HasKey(c => c.Id);
            this.Property(c => c.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            this.Property(c => c.Id).IsRequired();

            this.Property(c => c.ApplicationName).IsRequired();
            this.Property(c => c.ApplicationName).HasMaxLength(1000);


        }
    }
}
