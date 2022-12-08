using Microsoft.VisualBasic;
using System;
using System.Data.SqlClient;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Travelling.Models;

namespace Travelling.Services
{
    public class Database
    {
        private readonly SqlConnectionStringBuilder builder;
        private readonly GoogleMapsService googleMapsService;
        private readonly SqlConnection connection;

        private readonly object objectLock = new object();

        public Database(GoogleMapsService googleMapsService)
        {
            builder = new SqlConnectionStringBuilder();
            builder.DataSource = "localhost";
            builder.UserID = "root";
            builder.Password = "root";
            builder.InitialCatalog = "project";
            builder.MultipleActiveResultSets = true;
            connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            this.googleMapsService = googleMapsService;
        }


        public async Task<List<UserViewModel>> GetUsers()
        {
            string sql = "Select Id, Email, Password, Name, Surname, Phone from dbo.Users";
            List<UserViewModel> result = new List<UserViewModel>();

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        UserViewModel offer = new UserViewModel()
                        {
                            Id = reader.GetInt32(0),
                            Email = reader.GetString(1),
                            Password = reader.GetString(2),
                            Name = reader.GetString(3),
                            Surname = reader.GetString(4),
                            Phone = reader.GetString(5)
                        };

                        result.Add(offer);
                    }
                }
            }

            return result;
        }

        public async Task<UserViewModel> GetUser(string email)
        {
            string sql = $"Select Id, Email, Password, Name, Surname, Phone from dbo.Users where Email=\'{email}\'";
            List<UserViewModel> result = new List<UserViewModel>();

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    reader.Read();

                    UserViewModel user = new UserViewModel()
                    {
                        Id = reader.GetInt32(0),
                        Email = reader.GetString(1),
                        Password = reader.GetString(2),
                        Name = reader.GetString(3),
                        Surname = reader.GetString(4),
                        Phone = reader.GetString(5)
                    };

                    return user;
                }
            }
        }

        public async Task<UserViewModel> GetUser(int id)
        {
            string sql = $"Select Id, Email, Password, Name, Surname, Phone from dbo.Users where Id={id}";
            List<UserViewModel> result = new List<UserViewModel>();

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    reader.Read();

                    UserViewModel user = new UserViewModel()
                    {
                        Id = reader.GetInt32(0),
                        Email = reader.GetString(1),
                        Password = reader.GetString(2),
                        Name = reader.GetString(3),
                        Surname = reader.GetString(4),
                        Phone = reader.GetString(5)
                    };

                    return user;
                }
            }
        }

        public async Task<List<HousingOffer>> GetHousings()
        {
            string sql = "SELECT Id, LocationId, ApiId, Name, Description, OwnerId FROM dbo.HousingOffers";
            List<HousingOffer> housings = new List<HousingOffer>();

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);

                        HousingOffer offer = new HousingOffer()
                        {
                            Id = id,
                            LocationId = reader.GetInt32(1),
                            ApiId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                            Name = reader.GetString(3),
                            Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                            Images = new List<Models.Image>(),
                            OwnerId = reader.IsDBNull(5) ? null : reader.GetInt32(5)
                        };

                        housings.Add(offer);
                    }
                }
            }

            foreach (var item in housings)
            {
                string imagesSql = $"select dbo.Images.Id, FilePath from dbo.HousingImages left join dbo.Images on ImageId = dbo.Images.Id where HousingOfferId={item.Id}";
                SqlCommand imagesCommand = new SqlCommand(imagesSql, connection);

                using (SqlDataReader imagesReader = await imagesCommand.ExecuteReaderAsync())
                {
                    while (imagesReader.Read())
                    {
                        item.Images.Add(new Models.Image()
                        {
                            Id = imagesReader.GetInt32(0),
                            Uri = imagesReader.GetString(1)
                        });
                    }
                }

                item.Options = await GetHousingOptions(item, connection);
                item.Location = await GetLocation(item.LocationId, connection);
            }

            return housings;
        }

        public async Task<HousingOffer> GetOffer(int id)
        {
            string sql = $"SELECT Id, LocationId, ApiId, Name, Description FROM dbo.HousingOffers where Id={id}";

            HousingOffer result = null;

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    reader.Read();

                    int offerId = reader.GetInt32(0);

                    result = new HousingOffer()
                    {
                        Id = offerId,
                        LocationId = reader.GetInt32(1),
                        ApiId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                        Name = reader.GetString(3),
                        Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Images = new List<Models.Image>()
                    };
                }
            }
 
            string imagesSql = $"select dbo.Images.Id, FilePath from dbo.HousingImages left join dbo.Images on ImageId = dbo.Images.Id where HousingOfferId={result.Id}";
            SqlCommand imagesCommand = new SqlCommand(imagesSql, connection);

            using (SqlDataReader imagesReader = await imagesCommand.ExecuteReaderAsync())
            {
                while (imagesReader.Read())
                {
                    result.Images.Add(new Models.Image()
                    {
                        Id = imagesReader.GetInt32(0),
                        Uri = imagesReader.GetString(1)
                    });
                }
            }

            result.Options = await GetHousingOptions(result, connection);
            result.Location = await GetLocation(result.LocationId, connection);

            return result;
        }

        public async Task Save(IEnumerable<HousingOffer> offers)
        {
            foreach (var offer in offers)
            {
                await Save(offer);
            }
        }

        public async Task Save(HousingOffer offer)
        {
            await Save(offer.Location);

            string name = offer.Name.Replace("\'", "\'\'");
            string apiId = offer.Id.HasValue ? $"\'{offer.Id}\'" : "NULL";
            string ownerId = offer.OwnerId.HasValue ? offer.OwnerId.ToString() : "NULL";
            string description = offer.Description != null ? $"\'{offer.Description}\'" : "NULL";

            string sql = $"insert into dbo.HousingOffers(Name, LocationId, ApiId, Description, OwnerId) values (\'{name}\', {offer.Location.Id}, {apiId}, {description}, {ownerId}); select scope_identity()";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                offer.Id = (int)(decimal)await command.ExecuteScalarAsync();
            }

            foreach (var image in offer.Images)
            {
                await SaveHousingImage(offer, image);
            }

            foreach (var option in offer.Options)
            {
                await Save(option, offer);
            }
        }

        public async Task Save(UserViewModel userViewModel)
        {
            string sql = "insert into dbo.Users(Email, Password, Name, Surname, Phone) " +
                $"values (\'{userViewModel.Email}\', \'{userViewModel.Password}\', " +
                $"\'{userViewModel.Name}\', \'{userViewModel.Surname}\', \'{userViewModel.Phone}\'); " +
                "select scope_identity()";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                userViewModel.Id = (int)(decimal) await command.ExecuteScalarAsync();
            }
        }

        public async Task Save(Reservation reservation)
        {
            string dateFormat = "yyyy-MM-d";

            string sql = "insert into dbo.Reservations(StartDate, EndDate, UserId, HousingOptionId) values " +
                            $"(\'{reservation.StartDate.ToString(dateFormat)}\', " +
                            $"\'{reservation.EndDate.ToString(dateFormat)}\', " +
                            $"{reservation.UserId}, {reservation.Option.Id}); " +
                            "select scope_identity()";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                reservation.Id = (int)(decimal)await command.ExecuteScalarAsync();
            }
        }

        public async Task Save(ReservationNotification notification)
        {
            string sql = "insert into dbo.ReservationNotifications(ReservationId) values " +
                            $"({notification.Reservation.Id})" +
                            "select scope_identity()";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                notification.Id = (int)(decimal)await command.ExecuteScalarAsync();
            }
        }

        public async Task Update(HousingOffer model)
        {
            await Save(model.Location);

            string name = $"\'{model.Name.Replace("\'", "\'\'")}\'";
            string description = model.Description != null ? $"\'{model.Description}\'" : "NULL";

            string sql = "update dbo.HousingOffers " +
                $"SET Name = {name}, Description = {description}, LocationId = {model.Location.Id} " +
                $"Where Id = {model.Id}";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }

            string searchSql = $"select id from dbo.HousingOptions where HousingId = {model.Id}";
            List<int> optionIds = new List<int>(model.Options.Count);

            SqlCommand imagesCommand = new SqlCommand(searchSql, connection);

            using (SqlDataReader imagesReader = await imagesCommand.ExecuteReaderAsync())
            {
                while (imagesReader.Read())
                {
                    optionIds.Add(imagesReader.GetInt32(0));
                }
            }

            string whereClause = string.Join(" Or ", optionIds.Select(id => $"OptionId = {id}"));
            string deleteHousingImagesSql = $"Delete from dbo.HousingImages where HousingOfferId = {model.Id};" +
                $"delete from dbo.OptionImages where {whereClause};";

            using (SqlCommand deleteCommand = new SqlCommand(deleteHousingImagesSql, connection))
            {
                await deleteCommand.ExecuteNonQueryAsync();
            }

            foreach (var image in model.Images)
            {
                await SaveHousingImage(model, image);
            }

            for (int i = 0; i < model.Options.Count || i < optionIds.Count; i++)
            {
                if (i < model.Options.Count && i < optionIds.Count)
                {
                    string optionName = $"\'{model.Options[i].Name.Replace("\'", "\'\'")}\'";
                    string optionDescription = model.Options[i].Description != null ? $"\'{model.Description}\'" : "NULL";

                    string optionUpdate = "update dbo.HousingOptions " +
                      $"SET Name = {optionName}, Description = {optionDescription}, Price = {model.Options[i].Price}, " +
                      $"BedsAmount = {model.Options[i].BedsAmount}, MetersAmount = {model.Options[i].MetersAmount} Where Id = {optionIds[i]}";

                    using (SqlCommand optionCommand = new SqlCommand(optionUpdate, connection))
                    {
                        await optionCommand.ExecuteNonQueryAsync();
                    }

                    foreach (var option in model.Options)
                    {
                        foreach (var image in option.Images)
                        {
                            await SaveOptionImage(option, image);
                        }
                    }
                }
                else if (i < model.Options.Count && i >= optionIds.Count)
                {
                    await Save(model.Options[i], model);
                }
                else 
                {
                    string deleteSql = $"delete from dbo.housingOptions where id = {optionIds[i]};";

                    using (SqlCommand deleteCommand = new SqlCommand(deleteSql, connection))
                    {
                        await deleteCommand.ExecuteNonQueryAsync();
                    }
                }
            }


          
        }

        public bool DoesOfferExists(string name)
        {
            lock (objectLock)
            {
                name = name.Replace("\'", "\'\'");
                string query = $"select count(*) from dbo.housingOffers where name = \'{name}\'";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    int count = (int)command.ExecuteScalar();

                    return count > 0;
                }
            }
        }

        public bool DoesOfferExists(long apiId)
        {
            lock (objectLock)
            {
                string query = $"select count(*) from dbo.housingOffers where apiId = {apiId}";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    int count = (int)command.ExecuteScalar();

                    return count > 0;
                }
            }
        }

        public int GetNextOfferId()
        {
            lock (objectLock)
            {
                string query = $"select max(id) from dbo.housingOffers";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    int count = (int)command.ExecuteScalar();

                    return count + 1;
                }
            }
        }

        private async Task<List<HousingOption>> GetHousingOptions(HousingOffer offer, SqlConnection connection)
        {
            string optionsSql = $"SELECT Id, ApiId, Name, Description, Price, BedsAmount, MetersAmount " +
                $"FROM dbo.HousingOptions where HousingId={offer.Id.Value}";
            SqlCommand optionsCommand = new SqlCommand(optionsSql, connection);

            var results = new List<HousingOption>();

            using (SqlDataReader optionsReader = await optionsCommand.ExecuteReaderAsync())
            {
                while (optionsReader.Read())
                {
                    results.Add(new HousingOption()
                    {
                        Id = optionsReader.GetInt32(0),
                        ApiId = optionsReader.IsDBNull(1) ? null : optionsReader.GetString(1),
                        Name = optionsReader.GetString(2),
                        Description = optionsReader.IsDBNull(3) ? "" : optionsReader.GetString(3),
                        Price = optionsReader.GetDecimal(4),
                        BedsAmount = optionsReader.GetInt32(5),
                        MetersAmount = optionsReader.GetInt32(6),
                        Offer = offer
                    });
                }
            }

            foreach (var item in results)
            {
                string imagesSql = $"select dbo.Images.Id, FilePath from dbo.OptionImages left join dbo.Images on ImageId = dbo.Images.Id where OptionId={item.Id}";
                SqlCommand imagesCommand = new SqlCommand(imagesSql, connection);

                using (SqlDataReader imagesReader = await imagesCommand.ExecuteReaderAsync())
                {
                    while (imagesReader.Read())
                    {
                        item.Images.Add(new Models.Image()
                        {
                            Id = imagesReader.GetInt32(0),
                            Uri = imagesReader.GetString(1)
                        });
                    }
                }

                string reservationSql = $"select Id, StartDate, EndDate, UserId from dbo.Reservations where housingOptionId = {item.Id}";
                SqlCommand reservationsCommand = new SqlCommand(reservationSql, connection);

                using (SqlDataReader reservationReader = await reservationsCommand.ExecuteReaderAsync())
                {
                    while (reservationReader.Read())
                    {
                        item.Reservations.Add(new Reservation()
                        {
                            Id = reservationReader.GetInt32(0),
                            StartDate = reservationReader.GetDateTime(1),
                            EndDate = reservationReader.GetDateTime(2),
                            UserId = reservationReader.GetInt32(3),
                            Option = item
                        });
                    }
                }
            }

            return results;
        }

        private async Task<Location> GetLocation(int id, SqlConnection connection)
        {
            string optionsSql = $"SELECT Address, Latitude, Longitude FROM dbo.Locations where Id={id}";
            SqlCommand locationReader = new SqlCommand(optionsSql, connection);

            using (SqlDataReader dataReader = await locationReader.ExecuteReaderAsync())
            {
                dataReader.Read();
                return new Location()
                {
                    Id = id,
                    Address = dataReader.GetString(0),
                    Latitude = (float)dataReader.GetDouble(1),
                    Longitude = (float)dataReader.GetDouble(2)
                };
            }
        }

        private async Task Save(Location location)
        {
            string address = location.Address.Replace("\'", "\'\'");
            string sql = $"insert into dbo.Locations(Address, Latitude, Longitude) values (\'{address}\', {location.Latitude.ToString(CultureInfo.InvariantCulture)}, {location.Longitude.ToString(CultureInfo.InvariantCulture)})" +
                "Select scope_identity()";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                location.Id = (int)(decimal) await command.ExecuteScalarAsync();
            }
        }

        private async Task SaveHousingImage(HousingOffer offer, Models.Image image)
        {
            await SaveImage(image, "HousingImages", "HousingOfferId", offer.Id.Value);
        }

        private async Task SaveOptionImage(HousingOption option, Models.Image image)
        {
            await SaveImage(image, "OptionImages", "OptionId", option.Id);
        }

        private async Task SaveImage(Models.Image image, string tableName, string idName, int ownerId)
        {
            string query = $"select count(*) from dbo.Images where FilePath = \'{image.Uri}\'";

            int id = -1;

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                int count = (int)command.ExecuteScalar();

                if (count > 0)
                {
                    string idQuery = $"select Id from dbo.Images where FilePath = \'{image.Uri}\'";

                    using (SqlCommand idCommand = new SqlCommand(idQuery, connection))
                    {
                        id = (int)await idCommand.ExecuteScalarAsync();
                    }
                }
            }

            if (id == -1)
            {
                string insertSql = $"insert into dbo.Images(FilePath) values (\'{image.Uri}\'); select scope_identity();";

                using (SqlCommand command = new SqlCommand(insertSql, connection))
                {
                    id = (int)(decimal)await command.ExecuteScalarAsync();
                }
            }

            string commandSql = $"insert into dbo.{tableName}(ImageId, {idName}) values ({id}, {ownerId})";
            using (SqlCommand command = new SqlCommand(commandSql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        public IEnumerable<Reservation> FilterReservations(IEnumerable<Reservation> reservations)
        {
            return reservations.Where(reservation =>
            {
                string query = $"select count(*) from dbo.ReservationNotifications where ReservationId = {reservation.Id}";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    int count = (int)command.ExecuteScalar();

                    return count > 0;
                }
            });
        }

        public void ClearNotifications(IEnumerable<Reservation> reservations)
        {
            foreach (var reservation in reservations)
            {
                string query = $"delete from dbo.ReservationNotifications where ReservationId = {reservation.Id}";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private async Task Save(HousingOption option, HousingOffer offer)
        {
            string name = option.Name.Replace("\'", "\'\'");
            string description = option.Description != null ? $"\'{option.Description.Replace("\'", "\'\'")}\'" : "NULL";
            string apiId = option.ApiId != null ? $"\'{option.ApiId}\'" : "NULL";

            string sql = $"insert into dbo.HousingOptions(ApiId, Name, Description, Price, BedsAmount, MetersAmount, HousingId) values ({apiId}, \'{name}\', {description}, {option.Price}, {option.BedsAmount}, {option.MetersAmount}, {offer.Id}); select scope_identity()";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                option.Id = (int)(decimal) await command.ExecuteScalarAsync();
            }

            foreach (var optionImage in option.Images)
            {
                await SaveOptionImage(option, optionImage);
            }
        }
    }


}
