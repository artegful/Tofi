using System.Globalization;
using System.Net.Http;
using System.Text.Json.Nodes;
using Travelling.Models;

namespace Travelling.Services
{
    public class YandexMapsService
    {
        private const string API_KEY = "13ec04bf-ca25-4f6c-9c46-59cd2fb87a1f";
        private const int SEARCH_RADIUS = 50;
        private const int LIMIT = 20;

        private readonly HttpClient client;
        private readonly GoogleMapsService googleMapsService;

        public YandexMapsService(GoogleMapsService googleMapsService)
        {
            client = new HttpClient();
            this.googleMapsService = googleMapsService;
        }

        public async Task<Location> GetClosestSettlement(Location coordinates)
        {
            string apiUrl = $"https://api.rasp.yandex.net/v3.0/nearest_settlement/?apikey={API_KEY}&" +
                $"lat={coordinates.Latitude.ToString(CultureInfo.InvariantCulture)}&" +
                $"lng={coordinates.Longitude.ToString(CultureInfo.InvariantCulture)}&" +
                $"distance={SEARCH_RADIUS}&" +
                $"format=json";
            HttpResponseMessage response = await client.GetAsync(apiUrl);

            JsonNode root = JsonObject.Parse(await response.Content.ReadAsStringAsync());

            Location result = new Location()
            {
                Address = (string)root["title"],
                Latitude = (float)root["lat"],
                Longitude = (float)root["lng"],
                Code = (string)root["code"]
            };

            return result;
        }

        public async Task<IEnumerable<TripOffer>> GetTripOffers(Location departure, Location arrival, DateTime departureDate)
        {
            string apiUrl = $"https://api.rasp.yandex.net/v3.0/search/?" +
                $"apikey={API_KEY}&" +
                $"from={departure.Code}&" +
                $"to={arrival.Code}&" +
                $"date={departureDate.ToString("yyyy-MM-dd")}&" +
                "format=json&" +
                $"limit={LIMIT}";
            string jsonResult = await client.GetStringAsync(apiUrl);
            JsonNode root = JsonObject.Parse(jsonResult);

            JsonArray segments = (JsonArray)root["segments"];
            List<TripThread> threads = new List<TripThread>();
            List<TripOffer> result = new List<TripOffer>(segments.Count);

            foreach (JsonObject trip in segments)
            {
                string threadId = (string)trip["thread"]["uid"];
                TripThread? tripThread = threads.FirstOrDefault(th => th.ApiId == threadId);

                if (tripThread == null)
                {
                    tripThread = new TripThread()
                    {
                        ApiId = threadId,
                        Name = (string)trip["thread"]["title"],
                        Type = Enum.Parse<TripType>((string)trip["thread"]["transport_type"], true)
                    };

                    threads.Add(tripThread);
                }

                string departureAddress = (string)trip["from"]["title"];
                string arrivalAddress = (string)trip["to"]["title"];

                Task<(Location, string?)> departureTask = googleMapsService.GetLocationByAddress(departureAddress);
                Task<(Location, string?)> arrivalTask = googleMapsService.GetLocationByAddress(arrivalAddress);

                await Task.WhenAll(departureTask, arrivalTask);

                TripOffer item = new TripOffer()
                {
                    DepartureLocation = new Location()
                    {
                        Address = (string)trip["from"]["title"],
                        Latitude = departureTask.Result.Item1.Latitude,
                        Longitude = departureTask.Result.Item1.Longitude,
                        Code = (string)trip["from"]["code"]
                    },
                    ArrivalLocation = new Location()
                    {
                        Address = (string)trip["to"]["title"],
                        Latitude = arrivalTask.Result.Item1.Latitude,
                        Longitude = arrivalTask.Result.Item1.Longitude,
                        Code = (string)trip["to"]["code"]
                    },
                    DepartureDate = DateTime.Parse((string)trip["departure"]),
                    ArrivalDate = DateTime.Parse((string)trip["arrival"]),
                    TripThread = tripThread
                };

                if (trip["tickets_info"] != null && trip["tickets_info"]["places"] is JsonArray places && places.Count > 0 &&
                    places[0]["price"] != null)
                {
                    JsonNode? price = places[0]["price"]["whole"];

                    if (price != null)
                    {
                        item.Price = (decimal)price;
                    }
                }

                result.Add(item);
            }

            return result;
        }
    }
}
