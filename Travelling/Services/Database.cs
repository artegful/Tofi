using Microsoft.AspNetCore.Mvc.Rendering;
using System.Data.SqlClient;
using System.Globalization;
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
            string deployConnectionString = "Data Source=SQL8003.site4now.net;Initial Catalog=db_a91503_mssql;User Id=db_a91503_mssql_admin;Password=rootroot11;MultipleActiveResultSets=True";
            connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            this.googleMapsService = googleMapsService;
        }


        public async Task<List<User>> GetUsers()
        {
            string sql = "Select dbo.Users.Id, Email, Password, Name, Surname, Phone, dbo.Admins.Id from dbo.Users left join dbo.Admins on dbo.Admins.Id = dbo.Users.Id";
            List<User> result = new List<User>();

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        User offer = new User()
                        {
                            Id = reader.GetInt32(0),
                            Email = reader.GetString(1),
                            Password = reader.GetString(2),
                            Name = reader.GetString(3),
                            Surname = reader.GetString(4),
                            Phone = reader.GetString(5),
                            IsAdmin = !reader.IsDBNull(6)
                        };

                        result.Add(offer);
                    }
                }
            }

            return result;
        }

        public async Task<User> GetUser(string email)
        {
            string selectSql = $"select count(*) from dbo.Users where Email=\'{email}\'";

            using (SqlCommand countCommand = new SqlCommand(selectSql, connection))
            {
                int count = (int)await countCommand.ExecuteScalarAsync();

                if (count == 0)
                {
                    return null;
                }
            }

            string sql = $"Select dbo.Users.Id, Email, Password, Name, Surname, Phone, dbo.Admins.Id from dbo.Users left join dbo.Admins on dbo.Admins.Id = dbo.Users.Id where Email=\'{email}\'";
            List<User> result = new List<User>();

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    reader.Read();

                    User user = new User()
                    {
                        Id = reader.GetInt32(0),
                        Email = reader.GetString(1),
                        Password = reader.GetString(2),
                        Name = reader.GetString(3),
                        Surname = reader.GetString(4),
                        Phone = reader.GetString(5),
                        IsAdmin = !reader.IsDBNull(6)
                    };

                    string isAdmin = $"select count(*) from dbo.Admins where Id = {user.Id}";

                    using (SqlCommand isAdminCommand = new SqlCommand(isAdmin, connection))
                    {
                        user.IsAdmin = (int)(await isAdminCommand.ExecuteScalarAsync()) > 0;
                    }

                    return user;
                }
            }
        }

        public async Task<User> GetUser(int id)
        {
            string sql = $"Select dbo.Users.Id, Email, Password, Name, Surname, Phone, dbo.Admins.Id from dbo.Users " +
                $"left join dbo.Admins on dbo.Admins.Id = dbo.Users.Id where dbo.Users.Id={id}";

            List<User> result = new List<User>();

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    reader.Read();

                    User user = new User()
                    {
                        Id = reader.GetInt32(0),
                        Email = reader.GetString(1),
                        Password = reader.GetString(2),
                        Name = reader.GetString(3),
                        Surname = reader.GetString(4),
                        Phone = reader.GetString(5),
                        IsAdmin = !reader.IsDBNull(6)
                    };

                    return user;
                }
            }
        }

        public async Task<SelectList> GetDocumentTypes()
        {
            var dict = await GetDocumentDict();
            
            return new SelectList(dict.Select(pair => new { id = pair.Key, name = pair.Value }), "id", "name");
        }

        public async Task<Dictionary<int, string>> GetDocumentDict()
        {
            string sql = "select id, name from dbo.DocumentTypes";

            var result = new Dictionary<int, string>();
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        result[reader.GetInt32(0)] = reader.GetString(1);
                    }
                }
            }

            return result;
        }

        public async Task<List<HousingOffer>> GetHousings()
        {
            string sql = "SELECT Id, LocationId, Name, Description, OwnerId, Rating, GoogleId FROM dbo.HousingOffers";
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
                            Name = reader.GetString(2),
                            Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Images = new List<Models.Image>(),
                            OwnerId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                            Rating = reader.IsDBNull(5) ? null : (float) reader.GetDouble(5),
                            GoogleId = reader.IsDBNull(6) ? null : reader.GetString(6)
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

                item.Options = await GetHousingOptions(item);
                item.Location = await GetLocation(item.LocationId);
            }

            return housings;
        }

        public async Task<HousingOffer> GetOffer(int id)
        {
            string sql = $"SELECT LocationId, Name, Description, OwnerId, Rating, GoogleId FROM dbo.HousingOffers where Id={id}";

            HousingOffer result = null;

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    reader.Read();

                    result = new HousingOffer()
                    {
                        Id = id,
                        LocationId = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        OwnerId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                        Rating = 4.3f,
                        GoogleId = reader.IsDBNull(5) ? null : reader.GetString(5),
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

            result.Options = await GetHousingOptions(result);
            result.Location = await GetLocation(result.LocationId);

            return result;
        }

        public async Task<TripOffer> GetTripOffer(int id)
        {
            string sql = @$"
                    SELECT [Id]
                          ,[DepartureDateTime]
                          ,[ArrivalDateTime]
                          ,[Price]
                          ,[ThreadId]
                          ,[DepartureLocationId]
                          ,[ArrivalLocationId]
                      FROM [dbo].[TripOffers] where Id={id}";

            SqlCommand command = new SqlCommand(sql, connection);

            TripOffer result;
            int? threadId;
            int? departureLocationId;
            int? arrivalLocationId;

            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                reader.Read();

                result = new TripOffer()
                {
                    Id = reader.GetInt32(0),
                    DepartureDate = reader.GetDateTime(1),
                    ArrivalDate = reader.GetDateTime(2),
                    Price = reader.IsDBNull(3) ? null : reader.GetDecimal(3)
                };

                threadId = reader.GetInt32(4);
                departureLocationId = reader.GetInt32(5);
                arrivalLocationId = reader.GetInt32(6);
            }

            result.DepartureLocation = await GetLocation(departureLocationId.Value);
            result.ArrivalLocation = await GetLocation(arrivalLocationId.Value);
            result.TripThread = await GetThread(threadId.Value);

            return result;
        }

        public async Task<TripThread> GetThread(int id)
        {
            string sql = $@"
            SELECT [Name]
                  ,[TransportType]
                  ,[ApiId]
              FROM [dbo].[TripThreads] where Id = {id}";

            SqlCommand command = new SqlCommand(sql, connection);
            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                reader.Read();

                return new TripThread()
                {
                    Id = id,
                    Name = reader.GetString(0),
                    Type = (TripType)reader.GetInt32(1),
                    ApiId = reader.IsDBNull(2) ? null : reader.GetString(2)
                };
            }
        }

        public async Task Log(int severity, string message)
        {
            string sql = $"insert into dbo.Logs(Severity, Message, Time) values ({severity}, \'{message.Replace("\'", "\'\'")}\', \'{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}\');";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<IEnumerable<TripReservation>> GetTripReservations(int ownerId)
        {
            string selectReservation = @$"SELECT [Id]
              ,[TripId]
          FROM [dbo].[TripReservations] where OwnerId = {ownerId}";

            User owner = await GetUser(ownerId);

            List<TripReservation> reservations = new List<TripReservation>();
            SqlCommand command = new SqlCommand(selectReservation, connection);
            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    int tripId = reader.GetInt32(1);

                    TripReservation reservation = new TripReservation()
                    {
                        Id = id,
                        Owner = owner,
                        TripOffer = await GetTripOffer(tripId),
                        Passengers = new List<Passenger>()
                    };

                    string sql = @$"SELECT [Id]
                      ,[Name]
                      ,[Surname]
                      ,[MiddleName]
                      ,[DocumentCode]
                      ,[BirthDate]
                      ,[DocumentTypeId]
                  FROM [dbo].[PassengerInfos] where tripReservationId = {id}";

                    SqlCommand passengerCommand = new SqlCommand(sql, connection);
                    using (SqlDataReader passengerReader = await passengerCommand.ExecuteReaderAsync())
                    {
                        while (passengerReader.Read())
                        {
                            reservation.Passengers.Add(new Passenger()
                            {
                                Id = passengerReader.GetInt32(0),
                                Name = passengerReader.GetString(1),
                                Surname = passengerReader.GetString(2),
                                MiddleName = passengerReader.GetString(3),
                                DocumentCode = passengerReader.GetString(4),
                                BirthDate = passengerReader.GetDateTime(5),
                                DocumentType = passengerReader.GetInt32(6)
                            });
                        }
                    }

                    reservations.Add(reservation);
                }
            }

            return reservations;
        }

        public async Task<TripReservation> GetTripReservation(int id)
        {
            string selectReservation = @$"SELECT [OwnerId]
              ,[TripId]
          FROM [dbo].[TripReservations] where Id = {id}";

            TripReservation reservation = null;
            SqlCommand command = new SqlCommand(selectReservation, connection);
            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                reader.Read();

                int ownerId = reader.GetInt32(0);
                int tripId = reader.GetInt32(1);

                User owner = await GetUser(ownerId);

                reservation = new TripReservation()
                {
                    Id = id,
                    Owner = owner,
                    TripOffer = await GetTripOffer(tripId),
                    Passengers = new List<Passenger>()
                };

                string sql = @$"SELECT [Id]
                    ,[Name]
                    ,[Surname]
                    ,[MiddleName]
                    ,[DocumentCode]
                    ,[BirthDate]
                    ,[DocumentTypeId]
                FROM [dbo].[PassengerInfos] where tripReservationId = {id}";

                SqlCommand passengerCommand = new SqlCommand(sql, connection);
                using (SqlDataReader passengerReader = await passengerCommand.ExecuteReaderAsync())
                {
                    while (passengerReader.Read())
                    {
                        reservation.Passengers.Add(new Passenger()
                        {
                            Id = passengerReader.GetInt32(0),
                            Name = passengerReader.GetString(1),
                            Surname = passengerReader.GetString(2),
                            MiddleName = passengerReader.GetString(3),
                            DocumentCode = passengerReader.GetString(4),
                            BirthDate = passengerReader.GetDateTime(5),
                            DocumentType = passengerReader.GetInt32(6)
                        });
                    }
                }
            }

            return reservation;
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

            string sql = $"insert into dbo.HousingOffers(Name, LocationId, ApiId, Description, OwnerId, Rating, GoogleId) values " +
                $"(\'{name}\', {offer.Location.Id}, {apiId}, {description}, {ownerId}, {offer.Rating?.ToString(CultureInfo.InvariantCulture) ?? "NULL"}, {(offer.GoogleId != null ? $"\'{offer.GoogleId}\'" : "NULL")}); " +
                $"select scope_identity()";

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

        public async Task Save(User userViewModel)
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

        public async Task Save(TripReservation reservation)
        {
            string insertSql = $"insert into dbo.TripReservations(OwnerId, TripId) values " +
                $"({reservation.Owner.Id}, {reservation.TripOffer.Id});select scope_identity();";

            using (SqlCommand command = new SqlCommand(insertSql, connection))
            {
                reservation.Id = (int)(decimal)await command.ExecuteScalarAsync();
            }

            foreach (var passenger in reservation.Passengers)
            {
                await Save(reservation, passenger);
            }
        }

        public async Task CacheCity(string cityName)
        {
            string sql = $@"insert into dbo.CachedCities(Name) values ('{cityName.Replace("'\'", "\'\'")}');";
            
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<bool> IsCityCached(string name)
        {
            string sql = $@"select count(*) from dbo.CachedCities where Name = '{name.Replace("'\'", "\'\'")}'";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                return ((int)await command.ExecuteScalarAsync()) > 0;
            }
        }

        public async Task Save(TripReservation tripReservation, Passenger passenger)
        {
            string dateFormat = "yyyy-MM-d";

            string insertSql = @$"
INSERT INTO [dbo].[PassengerInfos]
           ([TripReservationId]
           ,[DocumentTypeId]
           ,[Name]
           ,[Surname]
           ,[MiddleName]
           ,[DocumentCode]
           ,[BirthDate])
     VALUES
           ({tripReservation.Id}
           ,{(int)passenger.DocumentType}
           ,'{passenger.Name}'
           ,'{passenger.Surname}'
           ,'{passenger.MiddleName}'
           ,'{passenger.DocumentCode}'
           ,'{passenger.BirthDate.ToString(dateFormat)}');";

            using (SqlCommand command = new SqlCommand(insertSql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task Save(Reservation reservation)
        {
            string dateFormat = "yyyy-MM-d";
            string sessionId = reservation.SessionId ?? "NULL";

            string sql = "insert into dbo.Reservations(StartDate, EndDate, UserId, HousingOptionId, SessionId, IsVerified) values " +
                            $"(\'{reservation.StartDate.ToString(dateFormat)}\', " +
                            $"\'{reservation.EndDate.ToString(dateFormat)}\', " +
                            $"{reservation.UserId}, {reservation.Option.Id}," +
                            $"\'{sessionId}\'," +
                            $"{(reservation.IsVerified ? 1 : 0)}); " +
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

        public async Task Delete(Reservation reservation)
        {
            string sql = $"delete from dbo.Reservations where id = {reservation.Id}";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task Delete(User user)
        {
            string sql = $"delete from dbo.Users where id = {user.Id}";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task Delete(HousingOffer offer)
        {
            string sql = $"delete from dbo.HousingOffers where id = {offer.Id}";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                await command.ExecuteNonQueryAsync();
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

        private async Task<List<HousingOption>> GetHousingOptions(HousingOffer offer)
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

                string reservationSql = $"select Id, StartDate, EndDate, UserId, SessionId, IsVerified from dbo.Reservations where housingOptionId = {item.Id}";
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
                            SessionId = reservationReader.GetString(4),
                            IsVerified = reservationReader.GetBoolean(5),
                            Option = item
                        });
                    }
                }
            }

            return results;
        }

        public async Task<bool> FilterOptions(int optionId, DateTime arrive, DateTime departure)
        {
            string dateFormat = "yyyy-MM-dd";

            string checkPendingRequestSql = @$"select count(*) from dbo.UnverifiedReservations where HousingOptionId = {optionId} " +
                $"and ArriveDate = '{arrive.ToString(dateFormat)}' and DepartureDate='{departure.ToString(dateFormat)}'";

            using (SqlCommand eraseCommand = new SqlCommand(checkPendingRequestSql, connection))
            {
                return ((int) await eraseCommand.ExecuteScalarAsync()) > 0;
            }
        }

        public async Task EraseTripReservation(int id)
        {
            string sql = $"delete from dbo.TripReservations where Id = {id}";

            using(SqlCommand eraseCommand = new SqlCommand(sql, connection))
            {
                await eraseCommand.ExecuteNonQueryAsync();
            }
        }

        private async Task<Location> GetLocation(int id)
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

        public async Task Save(IEnumerable<TripOffer> offers)
        {
            foreach (var offer in offers)
            {
                await Save(offer.TripThread);

                await Save(offer.DepartureLocation);
                await Save(offer.ArrivalLocation);

                string dateFormat = "yyyy-MM-d HH:mm:ss";
                string sql = $"select count(*) from dbo.TripOffers where ArrivalDateTime = \'{offer.ArrivalDate.ToString(dateFormat)}\' " +
                    $"and ThreadId = {offer.TripThread.Id}";

                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    int count = (int)command.ExecuteScalar();

                    if (count > 0)
                    {
                        string idQuery = $"select Id from dbo.TripOffers where ArrivalDateTime = '{offer.ArrivalDate.ToString(dateFormat)}' " +
                            $"and ThreadId = {offer.TripThread.Id}";

                        using (SqlCommand idCommand = new SqlCommand(idQuery, connection))
                        {
                            offer.Id = (int)await idCommand.ExecuteScalarAsync();
                        }
                    }
                    else
                    {
                        string query = @$"INSERT INTO [dbo].[TripOffers]
                                   ([DepartureLocationId]
                                   ,[ArrivalLocationId]
                                   ,[DepartureDateTime]
                                   ,[ArrivalDateTime]
                                   ,[Price]
                                   ,[ThreadId])
                             VALUES
                                   ({offer.DepartureLocation.Id}
                                   ,{offer.ArrivalLocation.Id}
                                   ,'{offer.DepartureDate.ToString(dateFormat)}'
                                   ,'{offer.ArrivalDate.ToString(dateFormat)}'
                                   ,{(offer.Price.HasValue ? offer.Price : "NULL")}
                                   ,{offer.TripThread.Id});" +
                            "Select scope_identity()";

                        using (SqlCommand insertCommand = new SqlCommand(query, connection))
                        {
                            offer.Id = (int)(decimal)await insertCommand.ExecuteScalarAsync();
                        }
                    }
                }
            }
        }

        private async Task Save(TripThread thread)
        {
            string sql = $"select count(*) from dbo.TripThreads where apiId = \'{thread.ApiId}\'";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                int count = (int)command.ExecuteScalar();

                if (count > 0)
                {
                    string idQuery = $"select Id from dbo.TripThreads where apiId = '{thread.ApiId}'";

                    using (SqlCommand idCommand = new SqlCommand(idQuery, connection))
                    {
                        thread.Id = (int)await idCommand.ExecuteScalarAsync();
                    }
                }
                else
                {
                    string query = $"insert into dbo.TripThreads(apiId, Name, TransportType) values (\'{thread.ApiId}\', \'{thread.Name}\', {(int)thread.Type});" +
                                    "Select scope_identity()";

                    using (SqlCommand insertCommand = new SqlCommand(query, connection))
                    {
                        thread.Id = (int)(decimal)await insertCommand.ExecuteScalarAsync();
                    }
                }
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

            string sql = $"insert into dbo.HousingOptions(ApiId, Name, Description, Price, BedsAmount, MetersAmount, HousingId) values ({apiId}, \'{name}\', {description}, {option.Price.ToString(CultureInfo.InvariantCulture)}, {option.BedsAmount}, {option.MetersAmount}, {offer.Id}); select scope_identity()";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                option.Id = (int)(decimal) await command.ExecuteScalarAsync();
            }

            foreach (var optionImage in option.Images)
            {
                await SaveOptionImage(option, optionImage);
            }
        }


        public HousingOption Images()
        {
            Random random = new Random();

            return new HousingOption()
            {
                Name = "Standard",
                Description = "Our standard room",
                Price = random.Next(50, 150),
                BedsAmount = 2,
                MetersAmount = random.Next(15, 30)
            };
        }
    }


}
