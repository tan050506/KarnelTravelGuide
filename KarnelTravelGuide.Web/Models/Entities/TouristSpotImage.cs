using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KarnelTravelGuide.Web.Models.Entities
{
    public class TouristSpotImage
    {
        [Key]
        public int ImageId { get; set; }

        public int SpotId { get; set; }

        [Required]
        public string? ImageUrl { get; set; }
        public string? Caption { get; set; }
        public virtual TouristSpot? TouristSpot { get; set; }
    }
}