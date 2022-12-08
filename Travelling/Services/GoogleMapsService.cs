using System.Net;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Travelling.Models;
using System.Text.Json;
using System.Globalization;
using System.Drawing;

namespace Travelling.Services
{
    public class GoogleMapsService
    {
        private readonly string apiKey = @"AIzaSyBjMY0DLNSioZkYPBFmFN0lHpR-pQwO0aM";
        private readonly HttpClient httpClient;

        public GoogleMapsService()
        {
            httpClient = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false });
        }

        public async Task<(Location, string?)> GetLocationByAddress(string address)
        {
            string result = await httpClient.GetStringAsync(@$"https://maps.googleapis.com/maps/api/geocode/json?address={address}&key={apiKey}");
            JsonNode root = JsonObject.Parse(result);
            JsonNode locationJson = root["results"][0]["geometry"]["location"];

            JsonArray addressComponents = (JsonArray)root["results"][0]["address_components"];

            string name = (string)addressComponents[0]["long_name"];

            return (new Location()
            {
                Id = 0,
                Address = address,
                Latitude = (float)locationJson["lat"],
                Longitude = (float)locationJson["lng"]
            }, name);
        }

        public async Task<string> GetResponseForHotelsAroundLocation(Location location, double radiusInKilometers = 50)
        {
            string request = @$"https://maps.googleapis.com/maps/api/place/nearbysearch/json?location={location.Latitude.ToString(CultureInfo.InvariantCulture)}%2C{location.Longitude.ToString(CultureInfo.InvariantCulture)}&radius={radiusInKilometers * 1000}&type=lodging&key={apiKey}";
            return await httpClient.GetStringAsync(request);
        }

        public string GetPhotoUrl(string reference)
        {
            string request = @$"https://maps.googleapis.com/maps/api/place/photo?maxwidth=800&photo_reference={reference}&key={apiKey}";

            HttpResponseMessage bitmapData = httpClient.GetAsync(request).Result;
            return bitmapData.Headers.Location.ToString();
        }
    }
}
