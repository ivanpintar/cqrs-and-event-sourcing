﻿using Newtonsoft.Json;
using PinetreeShop.CQRS.Infrastructure;
using PinetreeShop.CQRS.Infrastructure.Commands;
using PinetreeShop.CQRS.Infrastructure.Events;
using PinetreeShop.CQRS.Persistence;
using PinetreeShop.CQRS.Persistence.InMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PinetreeShop.Domain.Tests
{
    public abstract class ProcessManagerTestBase<TProcessManager> where TProcessManager : IProcessManager, new()
    {
        protected TestEventStore _eventStore = new TestEventStore();
        protected List<IEvent> _preConditions = new List<IEvent>();
        protected ProcessManagerRepository _processManagerRepository;

        protected abstract IDomainEntry BuildApplication();        

        protected void TearDown()
        {
            _preConditions.Clear();
        }

        protected void Given(params IEvent[] existingEvents)
        {
            _preConditions = existingEvents.ToList();
        }

        protected void When<TEvent>(TEvent command)
            where TEvent : IEvent
        {
            var app = BuildApplication();
            app.HandleEvent<TEvent, TProcessManager>(command);
        }

        protected void Then(params ICommand[] expectedCommands)
        {
            var latestCommands = _eventStore.GetLatestCommands();
            var expectedCommandsList = expectedCommands != null
                ? expectedCommands.ToList()
                : new List<ICommand>();

            Assert.Equal(latestCommands.Count(), expectedCommandsList.Count);

            var latestAndExpected = latestCommands.Zip(expectedCommandsList, (l, e) => new { L = l, E = e });

            foreach (var le in latestAndExpected)
            {
                Assert.True(ObjectsAreEqual(le.L, le.E));
            }

        }

        private bool ObjectsAreEqual(object evt1, object evt2)
        {
            var evtId = Guid.NewGuid();
            var now = DateTime.Now;

            // copy events to compare
            var obj1 = (dynamic)JsonConvert.DeserializeObject(JsonConvert.SerializeObject(evt1));
            var obj2 = (dynamic)JsonConvert.DeserializeObject(JsonConvert.SerializeObject(evt2));

            // since we're creating expected products manually, these won't ever match
            obj1.Metadata.Date = null;
            obj2.Metadata.Date = null;

            // this guid is created automatically, and we're comparing different objects, these will never match
            obj1.Metadata.EventId = null;
            obj2.Metadata.EventId = null;
            obj1.Metadata.CommandId = null;
            obj2.Metadata.CommandId = null;

            var json1 = JsonConvert.SerializeObject(obj1);
            var json2 = JsonConvert.SerializeObject(obj2);

            return json1 == json2;
        }
    }
}