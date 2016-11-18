﻿using PinetreeShop.CQRS.Infrastructure.Commands;
using PinetreeShop.CQRS.Infrastructure.Events;
using PinetreeShop.CQRS.Infrastructure.Repositories;
using PinetreeShop.Domain.Baskets;
using PinetreeShop.Domain.Baskets.Events;
using PinetreeShop.Domain.OrderProcess.Commands;
using PinetreeShop.Domain.Orders;
using PinetreeShop.Domain.Orders.Commands;
using PinetreeShop.Domain.Orders.Events;
using PinetreeShop.Domain.Products;
using PinetreeShop.Domain.Products.Commands;
using PinetreeShop.Domain.Products.Events;
using PinetreeShop.Domain.Shared.Types;
using System;
using System.Collections.Generic;
using Xunit;

namespace PinetreeShop.Domain.OrderProcess.Tests
{
    public class OrderProcessTests : OrderProcessTestBase
    {
        Guid orderId = Guid.NewGuid();
        Guid basketId = Guid.NewGuid();
        Guid productOneId = Guid.NewGuid();
        Guid productTwoId = Guid.NewGuid();
        Guid causationAndCorrelationId = Guid.NewGuid();
        Address shippingAddress = new Address { Country = "US", StateOrProvince = "CA", StreetAndNumber = "A2", ZipAndCity = "LA" };

        [Fact]
        public void When_BasketCheckedOut_ReserveProduct()
        {
            SetupInitialEvents();
            _initialEvents.ForEach(e => e.Item2.Metadata.CorrelationId = causationAndCorrelationId);

            Given(_initialEvents.ToArray());

            var evt = new BasketCheckedOut(basketId, shippingAddress);
            evt.Metadata.CorrelationId = causationAndCorrelationId;

            When(evt);

            var expectedCommands = new List<ICommand>
            {
                new ReserveProduct(productOneId, basketId, 10),
                new ReserveProduct(productTwoId, basketId, 20)
            };
            expectedCommands.ForEach(x =>
            {
                x.Metadata.CausationId = evt.Metadata.EventId;
                x.Metadata.CorrelationId = causationAndCorrelationId;
            });

            Then(expectedCommands.ToArray());
        }

        [Fact]
        public void When_SomeProductsReserved_NothingHappens()
        {
            SetupInitialEvents();
            AddInitialEvent<BasketAggregate>(new BasketCheckedOut(basketId, shippingAddress));
            _initialEvents.ForEach(e => e.Item2.Metadata.CorrelationId = causationAndCorrelationId);

            Given(_initialEvents.ToArray());

            var evt = new ProductReserved(productTwoId, basketId, 20);
            evt.Metadata.CorrelationId = causationAndCorrelationId;

            When(evt);

            Then(new ICommand[] { });
        }

        [Fact]
        public void When_AllProductsReserved_CreateOrder()
        {
            AggregateRepositoryBase.CreateGuid = () => orderId;
            SetupInitialEvents();
            AddInitialEvent<BasketAggregate>(new BasketCheckedOut(basketId, shippingAddress));
            AddInitialEvent<ProductAggregate>(new ProductReserved(productTwoId, basketId, 20));
            _initialEvents.ForEach(e => e.Item2.Metadata.CorrelationId = causationAndCorrelationId);

            Given(_initialEvents.ToArray());

            var evt = new ProductReserved(productOneId, basketId, 10);
            evt.Metadata.CorrelationId = causationAndCorrelationId;

            When(evt);

            var expectedCommand = new CreateOrder(orderId, basketId, OrderLines, shippingAddress);
            expectedCommand.Metadata.CausationId = evt.Metadata.EventId;
            expectedCommand.Metadata.CorrelationId = evt.Metadata.CorrelationId;

            Then(expectedCommand);
        }

        [Fact]
        public void When_SomeProductReservationFailed_NotifyAdmin()
        {
            var notificationId = Guid.NewGuid();
            AggregateRepositoryBase.CreateGuid = () => notificationId;
            SetupInitialEvents();
            AddInitialEvent<BasketAggregate>(new BasketCheckedOut(basketId, shippingAddress));
            AddInitialEvent<ProductAggregate>(new ProductReserved(productTwoId, basketId, 20));
            _initialEvents.ForEach(e => e.Item2.Metadata.CorrelationId = causationAndCorrelationId);

            Given(_initialEvents.ToArray());

            var evt = new ProductReservationFailed(productOneId, basketId, 10, ProductReservationFailed.NotAvailable);
            evt.Metadata.CorrelationId = causationAndCorrelationId;

            When(evt);

            var expectedCommand = new NotifyAdmin(notificationId);
            expectedCommand.Metadata.CausationId = evt.Metadata.EventId;
            expectedCommand.Metadata.CorrelationId = evt.Metadata.CorrelationId;

            Then(expectedCommand);
        }

        [Fact]
        public void When_CreateOrderFailed_NotifyAdmin()
        {
            var notificationId = Guid.NewGuid();
            AggregateRepositoryBase.CreateGuid = () => notificationId;
            SetupInitialEvents();
            AddInitialEvent<BasketAggregate>(new BasketCheckedOut(basketId, shippingAddress));
            AddInitialEvent<ProductAggregate>(new ProductReserved(productOneId, basketId, 10));
            AddInitialEvent<ProductAggregate>(new ProductReserved(productTwoId, basketId, 20));
            _initialEvents.ForEach(e => e.Item2.Metadata.CorrelationId = causationAndCorrelationId);

            Given(_initialEvents.ToArray());

            var evt = new CreateOrderFailed(orderId, basketId, EventFailedBase.UnknownError);
            evt.Metadata.CorrelationId = causationAndCorrelationId;

            When(evt);

            var expectedCommand = new NotifyAdmin(notificationId);
            expectedCommand.Metadata.CausationId = evt.Metadata.EventId;
            expectedCommand.Metadata.CorrelationId = evt.Metadata.CorrelationId;

            Then(expectedCommand);
        }

        [Fact]
        public void When_OrderCreated_DoNothing()
        {
            var notificationId = Guid.NewGuid();
            AggregateRepositoryBase.CreateGuid = () => notificationId;
            SetupInitialEvents();
            AddInitialEvent<BasketAggregate>(new BasketCheckedOut(basketId, shippingAddress));
            AddInitialEvent<ProductAggregate>(new ProductReserved(productOneId, basketId, 10));
            AddInitialEvent<ProductAggregate>(new ProductReserved(productTwoId, basketId, 20));
            _initialEvents.ForEach(e => e.Item2.Metadata.CorrelationId = causationAndCorrelationId);

            Given(_initialEvents.ToArray());

            var evt = new OrderCreated(orderId, basketId, OrderLines, shippingAddress);
            evt.Metadata.CorrelationId = causationAndCorrelationId;

            When(evt);
            Then(new ICommand[] { });
        }

        [Fact]
        public void When_OrderCancelled_CancelProductReservation_And_NotifyCustomer()
        {
            var notificationId = Guid.NewGuid();
            AggregateRepositoryBase.CreateGuid = () => notificationId;
            SetupInitialEvents();
            AddInitialEvent<BasketAggregate>(new BasketCheckedOut(basketId, shippingAddress));
            AddInitialEvent<ProductAggregate>(new ProductReserved(productOneId, basketId, 10));
            AddInitialEvent<ProductAggregate>(new ProductReserved(productTwoId, basketId, 20));
            _initialEvents.ForEach(e => e.Item2.Metadata.CorrelationId = causationAndCorrelationId);

            Given(_initialEvents.ToArray());

            var evt = new OrderCancelled(orderId);
            evt.Metadata.CorrelationId = causationAndCorrelationId;

            When(evt);

            var expectedCommands = new List<ICommand>
            {
                new CancelProductReservation(productOneId, 10),
                new CancelProductReservation(productTwoId, 20),
                new NotifyCustomer(notificationId)
            };
            expectedCommands.ForEach(c =>
            {
                c.Metadata.CausationId = evt.Metadata.EventId;
                c.Metadata.CorrelationId = evt.Metadata.CorrelationId;
            });

            Then(expectedCommands.ToArray());
        }

        [Fact]
        public void When_OrderShipped_DecreaseProductQuantity_And_NotifyCustomer()
        {
            var notificationId = Guid.NewGuid();
            AggregateRepositoryBase.CreateGuid = () => notificationId;
            SetupInitialEvents();
            AddInitialEvent<BasketAggregate>(new BasketCheckedOut(basketId, shippingAddress));
            AddInitialEvent<ProductAggregate>(new ProductReserved(productOneId, basketId, 10));
            AddInitialEvent<ProductAggregate>(new ProductReserved(productTwoId, basketId, 20));
            AddInitialEvent<OrderAggregate>(new OrderCreated(orderId, basketId, OrderLines, shippingAddress));
            _initialEvents.ForEach(e => e.Item2.Metadata.CorrelationId = causationAndCorrelationId);

            Given(_initialEvents.ToArray());

            var evt = new OrderShipped(orderId, shippingAddress);
            evt.Metadata.CorrelationId = causationAndCorrelationId;

            When(evt);

            var expectedCommands = new List<ICommand>
            {
                new ChangeProductQuantity(productOneId, -10),
                new ChangeProductQuantity(productTwoId, -20),
                new NotifyCustomer(notificationId)
            };
            expectedCommands.ForEach(c =>
            {
                c.Metadata.CausationId = evt.Metadata.EventId;
                c.Metadata.CorrelationId = evt.Metadata.CorrelationId;
            });

            Then(expectedCommands.ToArray());
        }

        [Fact]
        public void When_OrderDelivered_NotifyAdmin()
        {
            var notificationId = Guid.NewGuid();
            AggregateRepositoryBase.CreateGuid = () => notificationId;
            SetupInitialEvents();
            AddInitialEvent<BasketAggregate>(new BasketCheckedOut(basketId, shippingAddress));
            AddInitialEvent<ProductAggregate>(new ProductReserved(productOneId, basketId, 10));
            AddInitialEvent < ProductAggregate > (new ProductReserved(productTwoId, basketId, 20));
            AddInitialEvent<OrderAggregate>(new OrderCreated(orderId, basketId, OrderLines, shippingAddress));
            AddInitialEvent<OrderAggregate>(new OrderShipped(orderId, shippingAddress));
            _initialEvents.ForEach(e => e.Item2.Metadata.CorrelationId = causationAndCorrelationId);

            Given(_initialEvents.ToArray());

            var evt = new OrderDelivered(orderId, shippingAddress);
            evt.Metadata.CorrelationId = causationAndCorrelationId;

            When(evt);

            var expectedCommand = new NotifyAdmin(notificationId);
            expectedCommand.Metadata.CausationId = evt.Metadata.EventId;
            expectedCommand.Metadata.CorrelationId = evt.Metadata.CorrelationId;

            Then(expectedCommand);
        }

        private void SetupInitialEvents()
        {
            AddInitialEvent<BasketAggregate>(new BasketCreated(basketId));
            AddInitialEvent<BasketAggregate>(new BasketItemAdded(basketId, productOneId, "Test Product 1", 2, 10));
            AddInitialEvent<BasketAggregate>(new BasketItemAdded(basketId, productTwoId, "Test Product 2", 2, 20));
        }

        private List<OrderLine> OrderLines
        {
            get
            {
                return new List<OrderLine>
                {
                    new OrderLine { ProductId = productOneId, Price = 2, ProductName = "Test Product 1", Quantity = 10 },
                    new OrderLine { ProductId = productTwoId, Price = 2, ProductName = "Test Product 2", Quantity = 20 },
                };
            }
        }
    }
}