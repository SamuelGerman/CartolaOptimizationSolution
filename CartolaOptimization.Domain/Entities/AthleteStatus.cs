using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CartolaOptimization.Domain.Entities
{
    public class AthleteStatus
    {
        public AthleteStatus(int id, int cartolaId, string name)
        {
            Id = id;
            CartolaId = cartolaId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public int Id { get; set; }
        public int CartolaId { get; set; }
        public string Name { get; set; } = string.Empty;

        public ICollection<Athlete> Athletes { get; set; } = new List<Athlete>();

    }
}
