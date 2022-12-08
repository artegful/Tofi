namespace Travelling.Models
{
    using static Math;


    public class Location
    {
        public int? Id { get; set; }
        public string Address { get; set; }
        public float Latitude { get; set; }
        public float Longitude { get; set; }
        public string? Code { get; set; }


        public double GetDistance(Location other)
        {
            int earthRadius = 6371;

            var deltaLat = PI / 180 * (other.Latitude - Latitude);
            var deltaLong = PI / 180 * (other.Longitude - Longitude);

            double a = Sin(deltaLat / 2) * Sin(deltaLat / 2) + Cos(PI / 180 * Latitude) * Cos(PI/ 180 * other.Latitude) *
              Sin(deltaLong / 2) * Sin(deltaLong / 2);

            var c = 2 * Atan2(Sqrt(a), Sqrt(1 - a));

            var d = earthRadius * c;
            return d;
        }
    }
}
