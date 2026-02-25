using System.Net.Sockets;
using Axlebolt.RpcSupport.Protobuf;
using Axlebolt.Bolt.Protobuf;
using StandRiseServer.Core;
using StandRiseServer.Models;
using Google.Protobuf;
using MongoDB.Bson;
using MongoDB.Driver;

namespace StandRiseServer.Services;

/// <summary>
/// Сервис для управления купонами (промокодами)
/// Позволяет создавать, активировать и управлять купонами с наградами
/// </summary>
public class CouponService
{
    private readonly ProtobufHandler _handler;
    private readonly DatabaseService _database;
    private readonly SessionManager _sessionManager;

    public CouponService(ProtobufHandler handler, DatabaseService database, SessionManager sessionManager)
    {
        _handler = handler;
        _database = database;
        _sessionManager = sessionManager;

        Console.WriteLine("🎟️ Registering CouponService handlers...");
        _handler.RegisterHandler("CouponRemoteService", "createCoupon", CreateCouponAsync);
        _handler.RegisterHandler("CouponRemoteService", "activateCoupon", ActivateCouponAsync);
        _handler.RegisterHandler("CouponRemoteService", "getCouponInfo", GetCouponInfoAsync);
        _handler.RegisterHandler("CouponRemoteService", "deleteCoupon", DeleteCouponAsync);
        _handler.RegisterHandler("CouponRemoteService", "getMyCoupons", GetMyCouponsAsync);
        _handler.RegisterHandler("CouponRemoteService", "getActivatedCoupons", GetActivatedCouponsAsync);
        Console.WriteLine("🎟️ CouponService handlers registered!");
    }

    private Models.Player? GetCurrentPlayer(TcpClient client)
    {
        var session = _sessionManager.GetSessionByClient(client);
        if (session == null) return null;
        return _database.GetPlayerByTokenAsync(session.Token).Result;
    }

    private string GenerateCouponCode(int length = 12)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    /// <summary>
    /// Создание нового купона с наградами
    /// </summary>
    private async Task CreateCouponAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎟️ CreateCoupon Request");
            var player = GetCurrentPlayer(client);

            if (player == null)
            {
                await SendErrorAsync(client, request.Id, "Not authenticated");
                return;
            }

            // Парсим запрос
            var rewards = new List<RewardDefinition>();
            int maxUses = 1;
            int expirationDays = 30;
            string customCode = "";

            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var req = GenerateCouponRequest.Parser.ParseFrom(request.Params[0].One);
                
                foreach (var itemId in req.ItemDefinitionIds)
                {
                    rewards.Add(new RewardDefinition
                    {
                        Type = "item",
                        ItemDefinitionId = itemId,
                        Amount = 1
                    });
                }
                
                foreach (var currency in req.Currencies)
                {
                    rewards.Add(new RewardDefinition
                    {
                        Type = "currency",
                        CurrencyId = currency.CurrencyId,
                        Amount = (int)currency.Value
                    });
                }
            }

            // Дополнительные параметры
            if (request.Params.Count > 1 && request.Params[1].One != null)
                maxUses = Integer.Parser.ParseFrom(request.Params[1].One).Value;
            if (request.Params.Count > 2 && request.Params[2].One != null)
                expirationDays = Integer.Parser.ParseFrom(request.Params[2].One).Value;
            if (request.Params.Count > 3 && request.Params[3].One != null)
                customCode = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[3].One).Value;

            var couponCode = string.IsNullOrEmpty(customCode) ? GenerateCouponCode() : customCode.ToUpper();

            // Проверяем уникальность кода
            var couponCollection = _database.GetCollection<Models.Coupon>("coupons");
            var existingCoupon = await couponCollection.Find(c => c.Code == couponCode).FirstOrDefaultAsync();
            if (existingCoupon != null)
            {
                await SendErrorAsync(client, request.Id, "Coupon code already exists");
                return;
            }

            var coupon = new Models.Coupon
            {
                CouponId = Guid.NewGuid().ToString(),
                Code = couponCode,
                CreatorPlayerId = player.PlayerUid,
                Rewards = rewards,
                MaxUses = maxUses > 0 ? maxUses : 1,
                CurrentUses = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expirationDays > 0 ? DateTime.UtcNow.AddDays(expirationDays) : null
            };

            await couponCollection.InsertOneAsync(coupon);

            var response = new GenerateCouponResponse { CouponId = couponCode };
            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            
            Console.WriteLine($"🎟️ Created coupon: {couponCode} with {rewards.Count} rewards, maxUses={maxUses}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CreateCoupon: {ex.Message}");
            await SendErrorAsync(client, request.Id, ex.Message);
        }
    }

    /// <summary>
    /// Активация купона игроком
    /// </summary>
    private async Task ActivateCouponAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎟️ ActivateCoupon Request");
            var player = GetCurrentPlayer(client);

            if (player == null)
            {
                await SendActivateResponseAsync(client, request.Id, false, "Not authenticated");
                return;
            }

            string couponCode = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
            {
                var req = ActivateCouponRequest.Parser.ParseFrom(request.Params[0].One);
                couponCode = req.CouponId.ToUpper().Trim();
            }

            if (string.IsNullOrEmpty(couponCode))
            {
                await SendActivateResponseAsync(client, request.Id, false, "Invalid coupon code");
                return;
            }

            var couponCollection = _database.GetCollection<Models.Coupon>("coupons");
            var coupon = await couponCollection.Find(c => c.Code == couponCode).FirstOrDefaultAsync();

            // Валидация купона
            if (coupon == null)
            {
                await SendActivateResponseAsync(client, request.Id, false, "Coupon not found");
                return;
            }

            if (!coupon.IsActive)
            {
                await SendActivateResponseAsync(client, request.Id, false, "Coupon is not active");
                return;
            }

            if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < DateTime.UtcNow)
            {
                await SendActivateResponseAsync(client, request.Id, false, "Coupon has expired");
                return;
            }

            if (coupon.CurrentUses >= coupon.MaxUses)
            {
                await SendActivateResponseAsync(client, request.Id, false, "Coupon usage limit reached");
                return;
            }

            // Проверяем не использовал ли игрок уже этот купон
            var playerCouponCollection = _database.GetCollection<PlayerCoupon>("player_coupons");
            var existingUse = await playerCouponCollection.Find(pc => 
                pc.PlayerId == player.PlayerUid && pc.CouponId == coupon.CouponId).FirstOrDefaultAsync();

            if (existingUse != null)
            {
                await SendActivateResponseAsync(client, request.Id, false, "You have already used this coupon");
                return;
            }

            // Применяем награды
            foreach (var reward in coupon.Rewards)
            {
                if (reward.Type == "item" && reward.ItemDefinitionId > 0)
                {
                    var newItem = new PlayerInventoryItem
                    {
                        Id = player.Inventory.Items.Count > 0 ? player.Inventory.Items.Max(i => i.Id) + 1 : 1,
                        DefinitionId = reward.ItemDefinitionId,
                        Quantity = reward.Amount,
                        Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Flags = 0
                    };
                    player.Inventory.Items.Add(newItem);
                    Console.WriteLine($"✅ Coupon: Added item {reward.ItemDefinitionId} x{reward.Amount}");
                }
                else if (reward.Type == "currency")
                {
                    switch (reward.CurrencyId)
                    {
                        case 1: player.Coins += reward.Amount; break;
                        case 2: player.Gems += reward.Amount; break;
                        case 3: player.Keys += reward.Amount; break;
                        default:
                            if (!player.Inventory.Currencies.ContainsKey(reward.CurrencyId))
                                player.Inventory.Currencies[reward.CurrencyId] = 0;
                            player.Inventory.Currencies[reward.CurrencyId] += reward.Amount;
                            break;
                    }
                    Console.WriteLine($"✅ Coupon: Added currency[{reward.CurrencyId}] x{reward.Amount}");
                }
            }

            await _database.UpdatePlayerAsync(player);

            // Записываем использование
            var playerCoupon = new PlayerCoupon
            {
                PlayerId = player.PlayerUid,
                CouponId = coupon.CouponId,
                ActivatedAt = DateTime.UtcNow
            };
            await playerCouponCollection.InsertOneAsync(playerCoupon);

            // Увеличиваем счетчик
            var update = Builders<Models.Coupon>.Update.Inc(c => c.CurrentUses, 1);
            await couponCollection.UpdateOneAsync(c => c.CouponId == coupon.CouponId, update);

            await SendActivateResponseAsync(client, request.Id, true, "");
            Console.WriteLine($"✅ Activated coupon: {couponCode} for player {player.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ActivateCoupon: {ex.Message}");
            await SendActivateResponseAsync(client, request.Id, false, ex.Message);
        }
    }

    /// <summary>
    /// Получение информации о купоне по коду
    /// </summary>
    private async Task GetCouponInfoAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎟️ GetCouponInfo Request");

            string couponCode = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
                couponCode = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value.ToUpper().Trim();

            var couponCollection = _database.GetCollection<Models.Coupon>("coupons");
            var coupon = await couponCollection.Find(c => c.Code == couponCode).FirstOrDefaultAsync();

            if (coupon == null)
            {
                var emptyResult = new BinaryValue { IsNull = true };
                await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
                return;
            }

            var protoCoupon = ConvertToProtoCoupon(coupon);
            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(protoCoupon.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetCouponInfo: {ex.Message}");
            var emptyResult = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, emptyResult, null);
        }
    }

    /// <summary>
    /// Удаление купона (только создатель)
    /// </summary>
    private async Task DeleteCouponAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎟️ DeleteCoupon Request");
            var player = GetCurrentPlayer(client);

            if (player == null)
            {
                await SendErrorAsync(client, request.Id, "Not authenticated");
                return;
            }

            string couponCode = "";
            if (request.Params.Count > 0 && request.Params[0].One != null)
                couponCode = Axlebolt.RpcSupport.Protobuf.String.Parser.ParseFrom(request.Params[0].One).Value.ToUpper().Trim();

            var couponCollection = _database.GetCollection<Models.Coupon>("coupons");
            var coupon = await couponCollection.Find(c => c.Code == couponCode).FirstOrDefaultAsync();

            if (coupon == null)
            {
                await SendErrorAsync(client, request.Id, "Coupon not found");
                return;
            }

            // Только создатель может удалить купон
            if (coupon.CreatorPlayerId != player.PlayerUid)
            {
                await SendErrorAsync(client, request.Id, "You can only delete your own coupons");
                return;
            }

            // Деактивируем купон вместо удаления
            var update = Builders<Models.Coupon>.Update.Set(c => c.IsActive, false);
            await couponCollection.UpdateOneAsync(c => c.CouponId == coupon.CouponId, update);

            var result = new BinaryValue { IsNull = true };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"🎟️ Deleted coupon: {couponCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DeleteCoupon: {ex.Message}");
            await SendErrorAsync(client, request.Id, ex.Message);
        }
    }

    /// <summary>
    /// Получение купонов созданных игроком
    /// </summary>
    private async Task GetMyCouponsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎟️ GetMyCoupons Request");
            var player = GetCurrentPlayer(client);

            var response = new GetPlayerCouponsResponse();

            if (player != null)
            {
                var couponCollection = _database.GetCollection<Models.Coupon>("coupons");
                var coupons = await couponCollection
                    .Find(c => c.CreatorPlayerId == player.PlayerUid)
                    .SortByDescending(c => c.CreatedAt)
                    .Limit(50)
                    .ToListAsync();

                foreach (var coupon in coupons)
                {
                    response.Coupons.Add(ConvertToProtoCoupon(coupon));
                }
                response.TotalCount = response.Coupons.Count;
            }

            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"🎟️ GetMyCoupons: {response.TotalCount} coupons");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetMyCoupons: {ex.Message}");
            var response = new GetPlayerCouponsResponse();
            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }

    /// <summary>
    /// Получение активированных игроком купонов
    /// </summary>
    private async Task GetActivatedCouponsAsync(TcpClient client, RpcRequest request)
    {
        try
        {
            Console.WriteLine("🎟️ GetActivatedCoupons Request");
            var player = GetCurrentPlayer(client);

            var response = new GetPlayerCouponsResponse();

            if (player != null)
            {
                var playerCouponCollection = _database.GetCollection<PlayerCoupon>("player_coupons");
                var activatedCoupons = await playerCouponCollection
                    .Find(pc => pc.PlayerId == player.PlayerUid)
                    .SortByDescending(pc => pc.ActivatedAt)
                    .Limit(50)
                    .ToListAsync();

                var couponCollection = _database.GetCollection<Models.Coupon>("coupons");
                foreach (var pc in activatedCoupons)
                {
                    var coupon = await couponCollection.Find(c => c.CouponId == pc.CouponId).FirstOrDefaultAsync();
                    if (coupon != null)
                    {
                        response.Coupons.Add(ConvertToProtoCoupon(coupon));
                    }
                }
                response.TotalCount = response.Coupons.Count;
            }

            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
            Console.WriteLine($"🎟️ GetActivatedCoupons: {response.TotalCount} coupons");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetActivatedCoupons: {ex.Message}");
            var response = new GetPlayerCouponsResponse();
            var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
            await _handler.WriteProtoResponseAsync(client, request.Id, result, null);
        }
    }

    // Helper methods
    private Axlebolt.Bolt.Protobuf.Coupon ConvertToProtoCoupon(Models.Coupon coupon)
    {
        var protoCoupon = new Axlebolt.Bolt.Protobuf.Coupon
        {
            Id = coupon.CouponId,
            Code = coupon.Code,
            CreatedAt = new DateTimeOffset(coupon.CreatedAt).ToUnixTimeSeconds(),
            ExpiresAt = coupon.ExpiresAt.HasValue 
                ? new DateTimeOffset(coupon.ExpiresAt.Value).ToUnixTimeSeconds() 
                : 0,
            IsActive = coupon.IsActive && coupon.CurrentUses < coupon.MaxUses
        };

        foreach (var reward in coupon.Rewards.Where(r => r.Type == "item"))
        {
            protoCoupon.ItemDefinitionIds.Add(reward.ItemDefinitionId);
        }

        foreach (var reward in coupon.Rewards.Where(r => r.Type == "currency"))
        {
            protoCoupon.Currencies.Add(new CurrencyAmountCoupon
            {
                CurrencyId = reward.CurrencyId,
                Value = reward.Amount
            });
        }

        return protoCoupon;
    }

    private async Task SendErrorAsync(TcpClient client, string requestId, string message)
    {
        var response = new GenerateCouponResponse { CouponId = "" };
        var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
        await _handler.WriteProtoResponseAsync(client, requestId, result, null);
    }

    private async Task SendActivateResponseAsync(TcpClient client, string requestId, bool success, string errorMessage)
    {
        var response = new ActivateCouponResponse
        {
            Success = success,
            ErrorMessage = errorMessage
        };
        var result = new BinaryValue { IsNull = false, One = ByteString.CopyFrom(response.ToByteArray()) };
        await _handler.WriteProtoResponseAsync(client, requestId, result, null);
    }
}
