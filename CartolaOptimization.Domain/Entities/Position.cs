using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CartolaOptimization.Domain.Entities
{
    public class Position
    {
        public Position(int id, int cartolaId, string name, string abrevivation)
        {
            Id = id;
            CartolaId = cartolaId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Abreviation = abrevivation ?? throw new ArgumentNullException(nameof(abrevivation));
        }

        public int Id { get; set; }
        public int CartolaId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Abreviation { get; set; } = string.Empty;

        public ICollection<Athlete> Athletes { get; set; } = new List<Athlete>();
    }
}
