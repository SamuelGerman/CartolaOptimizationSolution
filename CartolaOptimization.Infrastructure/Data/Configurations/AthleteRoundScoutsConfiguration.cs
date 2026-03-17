using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public class AthleteRoundScoutsConfiguration : IEntityTypeConfiguration<AthleteRoundScouts>
{
    public void Configure(EntityTypeBuilder<AthleteRoundScouts> builder)
    {
        builder.ToTable("AthleteRoundScouts");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ScoutKey).IsRequired().HasMaxLength(5); // Ex: "G", "A", "DS"
        builder.Property(s => s.Valor).HasPrecision(10, 2);

        builder.HasOne(s => s.AthleteRound)
               .WithMany(ar => ar.Scouts)
               .HasForeignKey(s => s.AthleteRoundId)
               .OnDelete(DeleteBehavior.Cascade); // Se deletar o round, deleta os scouts
    }
}