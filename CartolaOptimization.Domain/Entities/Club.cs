using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CartolaOptimization.Domain.Entities
{
    public class Club
    {
        public Club()
        {
        }

        public Club(int id, int cartolaId, string abreviation, string slug, string nickname, string logoUrl)
        {
            Id = id;
            CartolaId = cartolaId;
            Abreviation = abreviation ?? throw new ArgumentNullException(nameof(abreviation));
            Slug = slug ?? throw new ArgumentNullException(nameof(slug));
            Nickname = nickname ?? throw new ArgumentNullException(nameof(nickname));
            LogoUrl = logoUrl ?? throw new ArgumentNullException(nameof(logoUrl));
        }

        public int Id { get; set; }
        public int CartolaId { get; set; }
        public string Abreviation { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public ICollection<Athlete> Athletes { get; set; } = new List<Athlete>();
        public ICollection<Game> Games { get; set; } = new List<Game>();
    }
}

