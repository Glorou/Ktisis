using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

using Ktisis.Data.Files.Util;

namespace Ktisis.Data.Files;

// Thanks ramen <3
/// <summary>Parses a human.pbd file to deform models. This file is located at <c>chara/xls/boneDeformer/human.pbd</c> in the game's data files.</summary>
public class PbdFile
{
    public readonly Header[] Headers;
    public readonly Link[] Links;                        // header.deformerId -> link
    public readonly Dictionary<int, Deformer> Deformers; // offset -> deformer
    private readonly byte[] data;
    public ReadOnlySpan<byte> RawData => this.data;

    public PbdFile(byte[] data) : this((ReadOnlySpan<byte>)data) { }

    public PbdFile(ReadOnlySpan<byte> data)
    {
        this.data = data.ToArray();
        var reader = new SpanBinaryReader(data);
        var entryCount = reader.ReadInt32();

        this.Headers = reader.Read<Header>(entryCount).ToArray();
        this.Links = reader.Read<Link>(entryCount).ToArray();
        this.Deformers = new Dictionary<int, Deformer>(entryCount);

        for (var i = 0; i < entryCount; i++)
        {
            var header = this.Headers[i];
            var offset = header.Offset;
            if (offset != 0)
            {
                reader.Seek(offset, SeekOrigin.Begin);
                var deformer = Deformer.Read(reader);
                this.Deformers[offset] = deformer;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Header
    {
        public ushort Id;
        public ushort DeformerId;
        public int Offset;
        public float Unk2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Link
    {
        // ushort.MaxValue = null
        public ushort ParentLinkIdx;
        public ushort FirstChildLinkIdx;
        public ushort NextSiblingLinkIdx;
        public ushort HeaderIdx;
    }

    public readonly struct DeformMatrix4X4
    {
        private readonly float[] matrix;

        public float this[int index]
        {
            get => index is >= 0 and < 16 ? this.matrix[index] : throw new IndexOutOfRangeException($"{index}");
            set
            {
                if (index is >= 0 and < 16) this.matrix[index] = value;
                else throw new IndexOutOfRangeException($"{index}");
            }
        }

        public DeformMatrix4X4(ReadOnlySpan<float> matrix) : this(matrix.ToArray()) { }

        public DeformMatrix4X4(float[] matrix)
        {
            if (matrix.Length != 16)
                throw new ArgumentException("Matrix must have 16 elements", nameof(matrix));

            this.matrix = matrix;
        }

        // https://github.com/TexTools/xivModdingFramework/blob/459b863fdacf291ee0817feef379a18275f33010/xivModdingFramework/Models/Helpers/ModelModifiers.cs#L1202
        public Vector3 TransformCoordinate(Vector3 vector) =>
            new(
                (vector.X * this.matrix[0]) + (vector.Y * this.matrix[1]) + (vector.Z * this.matrix[2]) + (1.0f * this.matrix[3]),
                (vector.X * this.matrix[4]) + (vector.Y * this.matrix[5]) + (vector.Z * this.matrix[6]) + (1.0f * this.matrix[7]),
                (vector.X * this.matrix[8]) + (vector.Y * this.matrix[9]) + (vector.Z * this.matrix[10]) + (1.0f * this.matrix[11])
            );
    }

    public struct Deformer
    {
        public int BoneCount;
        public string[] BoneNames;
        public DeformMatrix4X4?[] DeformMatrices;

        public static Deformer Read(SpanBinaryReader reader)
        {
            var start = reader.Position;
            var boneCount = reader.ReadInt32();

            var boneNames = new string[boneCount];
            var offsets = reader.Read<short>(boneCount);

            for (var i = 0; i < boneCount; i++)
            {
                var offset = offsets[i];
                var str = reader.ReadString(start + offset);
                boneNames[i] = str;
            }

            var padding = boneCount * 2 % 4;
            reader.Read<byte>(padding);

            var matrixArray = new DeformMatrix4X4?[boneCount];
            for (var i = 0; i < boneCount; i++)
            {
                var matrix = reader.Read<float>(12);
                var buf = new float[16];
                matrix.CopyTo(buf);
                buf[15] = 1.0f;
                matrixArray[i] = new DeformMatrix4X4(buf);
            }

            return new Deformer
            {
                BoneCount = boneCount,
                BoneNames = boneNames,
                DeformMatrices = matrixArray,
            };
        }

        public static DeformMatrix4X4 Identity()
        {
            var buf = new float[]
            {
                0, 0, 0, 0, // Translation (vec3 + unused)
                0, 0, 0, 1, // Rotation (vec4)
                1, 1, 1, 0,  // Scale (vec3 + unused)
                0, 0, 0, 1, // Affine
            };

            return new DeformMatrix4X4(buf);
        }
        
        public static Vector3 TransformCoordinate(Vector3 vector, DeformMatrix4X4 matrix) =>
            matrix.TransformCoordinate(vector);
    }

    public IEnumerable<Deformer> GetDeformers(ushort from, ushort to)
    {
        if (from == to)
            return Array.Empty<Deformer>();

        var deformerList = new List<Deformer>();
        var raceCodeList = new List<ushort>();
        var currentRaceCode = to;
        do
        {
            // raceCode (header) -> link -> parentLink -> raceCode (parentHeader)
            var header = this.Headers.First(h => h.Id == currentRaceCode);
            if (!this.Deformers.TryGetValue(header.Offset, out var deformer))
            {
                throw new InvalidOperationException($"Deformer does not exist for {currentRaceCode}");
            }

            deformerList.Add(deformer);
            raceCodeList.Add(currentRaceCode);

            var link = this.Links[header.DeformerId];
            if (link.ParentLinkIdx == ushort.MaxValue)
            {
                throw new InvalidOperationException($"Parent link does not exist for {currentRaceCode}");
            }

            var parentLink = this.Links[link.ParentLinkIdx];
            var parentHeader = this.Headers[parentLink.HeaderIdx];
            currentRaceCode = parentHeader.Id;
        }
        while (currentRaceCode != from);

        deformerList.Reverse();

        return deformerList;
    }
	
	private DeformMatrix4X4? ResolveDeformation(PbdFile.Deformer deformer, string name)
	{
		// Try and fetch it from the PBD
		var boneNames = deformer.BoneNames;
		var boneIdx = Array.FindIndex(boneNames, x => x == name);
		if (boneIdx != -1)
		{
			return deformer.DeformMatrices[boneIdx];
		}

		// Try and get it from the parent
		var boneNode = BoneMap.First(b => b.BoneName.Equals(name, StringComparison.Ordinal));
		if (boneNode.Parent != null)
		{
			var parent = boneNode.Parent as BoneNodeBuilder ??
				throw new InvalidOperationException("Parent isn't a bone node");
			return ResolveDeformation(deformer, parent.BoneName);
		}

		// No deformation, just use identity
		return PbdFile.Deformer.Identity();
	}
}