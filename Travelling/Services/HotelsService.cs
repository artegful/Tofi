using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection.PortableExecutable;
using System.Text.Json.Nodes;
using Travelling.Models;

namespace Travelling.Services
{
    public class HotelRequest
    {
        public Location Location { get; set; }
        public long Id { get; set; }
    }

    public class HotelsService
    {
        private const string API_KEY = "fcb6bb651amsh6f700808c3008c6p13e09fjsnef1694a1b822";

        private readonly HttpClient httpClient;
        private readonly Database database;

        public HotelsService(Database database)
        {
            httpClient = new HttpClient();
            this.database = database;
        }

        public async Task<List<HousingOffer>> GetOffers(string query)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"https://hotels4.p.rapidapi.com/locations/v3/search?q={query}&locale=en_US&langid=1033&siteid=300000001"),
                Headers =
                {
                    { "X-RapidAPI-Key", API_KEY },
                    { "X-RapidAPI-Host", "hotels4.p.rapidapi.com" }
                },
            };

            using (var response = await httpClient.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                return await ParseOffers(await response.Content.ReadAsStringAsync());
            }
        }

        public async Task<string> GetHotelSummary(long id)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://hotels4.p.rapidapi.com/properties/v2/get-summary"),
                Headers =
                {
                    { "X-RapidAPI-Key", API_KEY },
                    { "X-RapidAPI-Host", "hotels4.p.rapidapi.com" }
                },
                Content = new StringContent($"{{\r\"currency\": \"USD\",\r\"eapid\": 1,\r\"locale\": \"en_US\",\r\"siteId\": 300000001,\r\"propertyId\": \"{id}\"\r}}")
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }   
            };

            using (var response = await httpClient.SendAsync(request))
            {
	            response.EnsureSuccessStatusCode();
	            return await response.Content.ReadAsStringAsync();
            }
        }

        public async Task<string> GetHousingOptionsData(long id, DateTime checkin, DateTime checkout, int amountOfPeople)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://hotels4.p.rapidapi.com/properties/v2/get-offers"),
                Headers =
                {
                    { "X-RapidAPI-Key", API_KEY },
                    { "X-RapidAPI-Host", "hotels4.p.rapidapi.com" },
                },
                Content = new StringContent(
                @$"{{
                    ""currency"": ""USD"",
                    ""eapid"": 1,
                    ""locale"": ""en_US"",
                    ""siteId"": 300000001,
                    ""propertyId"": ""{id}"",
                    ""checkInDate"": {{
                        ""day"": {checkin.Day},
                        ""month"": {checkin.Month},
                        ""year"": {checkin.Year}
                    }},
                    ""checkOutDate"": {{
                        ""day"": {checkout.Day},
                        ""month"": {checkout.Month},
                        ""year"": {checkout.Year}
                    }},
                    ""destination"": {{
                        ""coordinates"": {{
                            ""latitude"": 0,
                            ""longitude"": 0
                        }},
                        ""regionId"": ""0""
                    }},
                    ""rooms"": [
                        {{
                            ""adults"": {amountOfPeople},
                            ""children"": []
                        }}
                    ]
                }}")
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };

            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }

        public async Task<List<HousingOffer>> ParseOffers(string data)
        {
            JsonNode root = JsonObject.Parse(data);
            JsonArray results = (JsonArray) root["sr"];

            List<HotelRequest> requests = new List<HotelRequest>(results.Count);

            foreach (var node in results)
            {
                if (requests.Count >= 5)
                {
                    break;
                }
                if ((string)node["type"] != "HOTEL")
                {
                    continue;
                }

                long id = int.Parse((string)node["hotelId"]);

                if (database.DoesOfferExists(id))
                {
                    continue;
                }

                requests.Add(new HotelRequest()
                {
                    Location = new Location()
                    {
                        Address = (string)node["hotelAddress"]["street"],
                        Latitude = float.Parse((string)node["coordinates"]["lat"], CultureInfo.InvariantCulture),
                        Longitude = float.Parse((string)node["coordinates"]["long"], CultureInfo.InvariantCulture)
                    },
                    Id = id
                });
            }

            if (requests.Count == 0)
            {
                return new List<HousingOffer>();
            }

            List<Task<string>> tasks = requests.Select(req => GetHotelSummary(req.Id)).ToList();
            await Task.WhenAll(tasks);

            List<HousingOffer> offers = new List<HousingOffer>(tasks.Count);

            for (int i = 0; i < tasks.Count; i++)
            {
                JsonNode rootNode = JsonObject.Parse(tasks[i].Result)["data"]["propertyInfo"];
                JsonNode summary = rootNode["summary"];

                string name = (string)summary["name"];

                if (database.DoesOfferExists(name))
                {
                    continue;
                }

                JsonArray images = (JsonArray)rootNode["propertyGallery"]["images"];

                HousingOffer offer = new HousingOffer()
                {
                    ApiId = long.Parse((string)summary["id"]),
                    Location = requests[i].Location,
                    Name = name,
                    Description = (string)summary["tagline"],
                    Images = new List<Image>(Math.Min(images.Count, 10)),
                    Options = new List<HousingOption>()
                };

                for(int j = 0; j < Math.Min(images.Count, 10); j++)
                {
                    offer.Images.Add(new Image()
                    {
                        Uri = (string)images[j]["image"]["url"]
                    });
                }

                offers.Add(offer);
            }

            DateTime freeCheckin = DateTime.Now.AddMonths(1);
            tasks = offers.Select(offer => GetHousingOptionsData(offer.ApiId.Value, freeCheckin, freeCheckin.AddDays(1), 1)).ToList();

            Task.WhenAll(tasks);

            for (int i = 0; i < tasks.Count; i++)
            {
                JsonArray options = (JsonArray)JsonObject.Parse(tasks[i].Result)["data"]["propertyOffers"]["units"];

                if (options.Count == 0)
                {
                    Random random = new Random();

                    offers[i].Options.Add(database.Images());
                }
                
                foreach (var option in options)
                {
                    decimal price;
                    int beds;
                    float meters;

                    try
                    {
                        JsonArray features = (JsonArray)option["features"];
                        var texts = features.Select(n => (string)n["text"]);

                        int bedsParsed = int.Parse(texts.First(t => t.Contains("Sleeps")).Split(' ')[1]);
                        int metersParsed = (int)Math.Floor(float.Parse(texts.First(t => t.Contains("sq")).Split(' ')[0]) / 10.76);

                        HousingOption resultOption = new HousingOption()
                        {
                            ApiId = (string)option["id"],
                            Name = (string)option["header"]["text"],
                            Description = (string)option["description"],
                            Images = new List<Image>(),
                            Price = (decimal)(float)option["ratePlans"][0]["priceDetails"][0]["price"]["total"]["amount"],
                            BedsAmount = bedsParsed,
                            MetersAmount = metersParsed
                        };

                        JsonArray images = (JsonArray)option["unitGallery"]["gallery"];
                        resultOption.Images.Capacity = images.Count;

                        foreach (var image in images)
                        {
                            resultOption.Images.Add(new Image()
                            {
                                Uri = (string)image["image"]["url"]
                            });
                        }

                        offers[i].Options.Add(resultOption);
                    }
                    catch { }
                }
            }

            return offers;
        }
    }
}
