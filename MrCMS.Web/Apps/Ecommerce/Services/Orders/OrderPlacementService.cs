using System;
using System.Linq;
using MrCMS.Helpers;
using MrCMS.Services;
using MrCMS.Web.Apps.Ecommerce.Entities.Orders;
using MrCMS.Web.Apps.Ecommerce.Helpers;
using MrCMS.Web.Apps.Ecommerce.Models;
using MrCMS.Web.Apps.Ecommerce.Services.Orders.Events;
using NHibernate;

namespace MrCMS.Web.Apps.Ecommerce.Services.Orders
{
    public class OrderPlacementService : IOrderPlacementService
    {
        private readonly ISession _session;
        private readonly IOrderLineCreator _orderLineCreator;
        private readonly IGiftCardApplier _giftCardApplier;

        public OrderPlacementService(ISession session, IOrderLineCreator orderLineCreator, IGiftCardApplier giftCardApplier)
        {
            _session = session;
            _orderLineCreator = orderLineCreator;
            _giftCardApplier = giftCardApplier;
        }

        public Order PlaceOrder(CartModel cartModel, Action<Order> postCreationActions)
        {
            // this code is here to try and take into account a 3rd party posting back more than once for whatever reason (see bug #2754). 
            // If the order with the guid has already been placed, we'll just return that placed order
            var existingOrders = _session.QueryOver<Order>().Where(order => order.Guid == cartModel.CartGuid).List();
            if (existingOrders.Any())
            {
                return existingOrders.First();
            }
            var placedOrder = _session.Transact(session =>
            {
                var order = new Order
                {
                    ShippingAddress = cartModel.RequiresShipping ? cartModel.ShippingAddress.ToAddressData(_session) : null,
                    BillingAddress = cartModel.BillingAddress.ToAddressData(_session),
                    ShippingMethodName = cartModel.RequiresShipping ? cartModel.ShippingMethod.Name : "No shipping required",
                    Subtotal = cartModel.Subtotal,
                    DiscountAmount = cartModel.OrderTotalDiscount,
                    Discount = cartModel.Discount,
                    DiscountCode = cartModel.DiscountCode,
                    RewardPointsAppliedAmount = cartModel.AppliedRewardPointsAmount,
                    Tax = cartModel.Tax,
                    Total = cartModel.Total,
                    TotalPaid = cartModel.TotalToPay,
                    ShippingTotal = cartModel.ShippingTotal,
                    ShippingTax = cartModel.ShippingTax,
                    User = cartModel.User,
                    Weight = cartModel.Weight,
                    OrderEmail = cartModel.OrderEmail,
                    GiftMessage = cartModel.GiftMessage,
                    CustomerIP = RequestHelper.GetIP(),
                    HttpData = RequestHelper.GetRawHttpData(),
                    PaymentMethod = cartModel.PaymentMethodSystemName,
                    ShippingStatus = cartModel.RequiresShipping ? ShippingStatus.Pending : ShippingStatus.ShippingNotRequired,
                    ShippingTaxPercentage = cartModel.ShippingTaxPercentage,
                    SalesChannel = EcommerceApp.DefaultSalesChannel,
                    Guid = cartModel.CartGuid,
                };
                session.Save(order);
                foreach (var orderLine in cartModel.Items.Select(item => _orderLineCreator.GetOrderLine(item)))
                {
                    orderLine.Order = order;
                    session.Save(orderLine);
                    order.OrderLines.Add(orderLine);
                }

                order = _giftCardApplier.Apply(cartModel.AppliedGiftCards, order);

                if (postCreationActions != null)
                    postCreationActions(order);

                // Similarly, we check again just before we save - we should be fine as we are inside of a transaction
                // but we will err on the side of catching duplicates
                existingOrders = _session.QueryOver<Order>().Where(o => o.Guid == cartModel.CartGuid).List();
                if (existingOrders.Any())
                {
                    return existingOrders.First();
                }

                session.Update(order);

                return order;
            });

            EventContext.Instance.Publish<IOnOrderPlaced, OrderPlacedArgs>(new OrderPlacedArgs { Order = placedOrder });
            return placedOrder;
        }
    }
}