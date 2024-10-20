using System;
using System.Threading.Tasks;
using Godot;

public class SpatialLookup2D
{
    static Vector2I[] offsets2D = {
        new Vector2I(-1, 1),
        new Vector2I( 0, 1),
        new Vector2I( 1, 1),
        new Vector2I(-1, 0),
        new Vector2I( 0, 0),
        new Vector2I( 1, 0),
        new Vector2I(-1,-1),
        new Vector2I( 0,-1),
        new Vector2I( 1,-1)
    };

    Vector2[] points;
    float radius;

    int[] spatialOffsets;
    Entry[] spatialIndices;

    public SpatialLookup2D(int numPoints)
    {
        spatialOffsets = new int[numPoints];
        spatialIndices = new Entry[numPoints];
    }

    public void Update(Vector2[] points, float radius)
    {
        this.points = points;
        this.radius = radius;

        Parallel.For(0, points.Length, i =>
        {
            Vector2I cell = GetCell2D(points[i], radius);
            uint key = KeyFromHash(HashCell2D(cell));
            spatialOffsets[i] = int.MaxValue;
            spatialIndices[i] = new Entry(i, key);
        });

        Array.Sort(spatialIndices);

        Parallel.For(0, points.Length, i =>
        {
            uint key = spatialIndices[i].key;
            uint keyPrev = i == 0 ? uint.MaxValue : spatialIndices[i - 1].key;
            if (key != keyPrev)
            {
                spatialOffsets[key] = i;
            }
        });
    }

    public Vector2I GetCell2D(Vector2 point, float radius)
    {
        return new Vector2I(
            (int)(point.X / radius),
            (int)(point.Y / radius)
        );
    }

    const uint hashK1 = 15823;
    const uint hashK2 = 9737333;
    public uint HashCell2D(Vector2I cell)
    {
        uint a = (uint)cell.X * hashK1;
        uint b = (uint)cell.Y * hashK2;
        return a + b;
    }

    public uint KeyFromHash(uint hash)
    {
        return hash % (uint)spatialIndices.Length;
    }

    public void ForeachPointWithinRadius(Vector2 samplePoint, Action<int> callback)
    {
        Vector2I originCell = GetCell2D(samplePoint, radius);
        float sqrRadius = radius * radius;

        foreach (Vector2I offset in offsets2D)
        {
            uint key = KeyFromHash(HashCell2D(originCell + offset));
            int currIndex = spatialOffsets[key];

            for (int i = currIndex; i < spatialIndices.Length; i++)
            {
                if (spatialIndices[i].key != key) break;

                int particleIndex = spatialIndices[i].index;
                float sqrDst = points[particleIndex].DistanceSquaredTo(samplePoint);

                if (sqrDst < sqrRadius)
                {
                    callback(particleIndex);
                }
            }
        }
    }

    public struct Entry : IComparable<Entry>
    {
        public int index;
        public uint key;

        public Entry(int index, uint key)
        {
            this.index = index;
            this.key = key;
        }

        public int CompareTo(Entry other)
        {
            return key.CompareTo(other.key);
        }
    }
}