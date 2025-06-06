// Copyright 2023, T. C. Raymond
// SPDX-License-Identifier: MIT

using System;
using System.Threading;

namespace GeometryLib
{
    public enum GeomEntityType
    {
        Point = 0,
        Line = 1,
        Arc = 2,
        LineLoop = 3,
        Surface = 4
    }

    public abstract class GeomEntity
    {
        private static int _nextId = 0;
        private static int _nextTag = 1; // Start tags at 1 (0 can mean "untagged" if desired)
        private readonly int _id;

        /// <summary>
        /// Unique, immutable ID for this geometry entity instance (starts at 0).
        /// </summary>
        public int Id => _id;

        /// <summary>
        /// Physical/material/boundary tag for this entity.
        /// Use this to assign region, material, or boundary condition.
        /// </summary>
        public int Tag { get; set; } = 0;

        public abstract GeomEntityType Type { get; }

        protected GeomEntity()
        {
            _id = Interlocked.Increment(ref _nextId) - 1; // Ensures IDs start at 0
        }

        /// <summary>
        /// Assigns the next available tag to this entity and returns it.
        /// </summary>
        public int AddTag()
        {
            int tag = Interlocked.Increment(ref _nextTag) - 1;
            Tag = tag;
            return tag;
        }

        public virtual bool Contains(GeomPoint point) { return false; }
    }
}