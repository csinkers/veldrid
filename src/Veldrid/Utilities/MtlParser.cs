﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Veldrid.Utilities;

/// <summary>
/// A parser for Wavefront MTL files.
/// </summary>
public class MtlParser
{
    readonly ParseContext _pc = new();

    /// <summary>
    /// Parses a <see cref="MtlFile"/> from the given array of text lines.
    /// </summary>
    /// <param name="lines">The raw text lines of the MTL file.</param>
    /// <returns>A new <see cref="MtlFile"/>.</returns>
    public MtlFile Parse(string[] lines)
    {
        foreach (string line in lines)
            _pc.Process(line);

        _pc.EndOfFileReached();
        return _pc.FinalizeFile();
    }

    /// <summary>
    /// Parses a <see cref="MtlFile"/> from the given stream
    /// </summary>
    /// <param name="s">The stream to parse from.</param>
    /// <returns>A new <see cref="MtlFile"/>.</returns>
    public MtlFile Parse(Stream s)
    {
        string text;
        using (StreamReader sr = new(s))
            text = sr.ReadToEnd();

        int lineStart = 0;
        int lineEnd;
        while ((lineEnd = text.IndexOf('\n', lineStart)) != -1)
        {
            string line = lineEnd != 0 && text[lineEnd - 1] == '\r'
                ? text.Substring(lineStart, lineEnd - lineStart - 1)
                : text[lineStart..lineEnd];

            _pc.Process(line);
            lineStart = lineEnd + 1;
        }

        _pc.EndOfFileReached();
        return _pc.FinalizeFile();
    }

    class ParseContext
    {
        static readonly char[] s_whitespaceChars = [' '];

        readonly List<MaterialDefinition> _definitions = [];
        MaterialDefinition? _currentDefinition;

        int _currentLine;
        string? _currentLineText;

        public void Process(string line)
        {
            _currentLine++;
            _currentLineText = line;

            string[] pieces = line.Split(s_whitespaceChars, StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length == 0 || pieces[0].StartsWith('#'))
            {
                return;
            }
            switch (pieces[0].ToLowerInvariant().Trim())
            {
                case "newmtl":
                    ExpectExactly(pieces, 1, "newmtl");
                    FinalizeCurrentMaterial();
                    _currentDefinition = new(pieces[1]);
                    break;

                case "ka":
                    ExpectExactly(pieces, 3, "Ka");
                    GetCurrentDefinition().AmbientReflectivity = ParseVector3(
                        pieces[1],
                        pieces[2],
                        pieces[3],
                        "Ka"
                    );
                    break;

                case "kd":
                    ExpectExactly(pieces, 3, "Kd");
                    GetCurrentDefinition().DiffuseReflectivity = ParseVector3(
                        pieces[1],
                        pieces[2],
                        pieces[3],
                        "Kd"
                    );
                    break;

                case "ks":
                    ExpectExactly(pieces, 3, "Ks");
                    GetCurrentDefinition().SpecularReflectivity = ParseVector3(
                        pieces[1],
                        pieces[2],
                        pieces[3],
                        "Ks"
                    );
                    break;

                case "ke": // Non-standard?
                    ExpectExactly(pieces, 3, "Ke");
                    GetCurrentDefinition().EmissiveCoefficient = ParseVector3(
                        pieces[1],
                        pieces[2],
                        pieces[3],
                        "Ks"
                    );
                    break;

                case "tf":
                    ExpectExactly(pieces, 3, "Tf");
                    GetCurrentDefinition().TransmissionFilter = ParseVector3(
                        pieces[1],
                        pieces[2],
                        pieces[3],
                        "Tf"
                    );
                    break;

                case "illum":
                    ExpectExactly(pieces, 1, "illum");
                    GetCurrentDefinition().IlluminationModel = ParseInt(pieces[1], "illum");
                    break;

                case "d": // "Dissolve", or opacity
                    ExpectExactly(pieces, 1, "d");
                    GetCurrentDefinition().Opacity = ParseFloat(pieces[1], "d");
                    break;

                case "tr": // Transparency
                    ExpectExactly(pieces, 1, "Tr");
                    GetCurrentDefinition().Opacity = 1 - ParseFloat(pieces[1], "Tr");
                    break;

                case "ns":
                    ExpectExactly(pieces, 1, "Ns");
                    GetCurrentDefinition().SpecularExponent = ParseFloat(pieces[1], "Ns");
                    break;

                case "sharpness":
                    ExpectExactly(pieces, 1, "sharpness");
                    GetCurrentDefinition().Sharpness = ParseFloat(pieces[1], "sharpness");
                    break;

                case "ni": // "Index of refraction"
                    ExpectExactly(pieces, 1, "Ni");
                    GetCurrentDefinition().OpticalDensity = ParseFloat(pieces[1], "Ni");
                    break;

                case "map_ka":
                    ExpectExactly(pieces, 1, "map_ka");
                    GetCurrentDefinition().AmbientTexture = pieces[1];
                    break;

                case "map_kd":
                    ExpectExactly(pieces, 1, "map_kd");
                    GetCurrentDefinition().DiffuseTexture = pieces[1];
                    break;

                case "map_ks":
                    ExpectExactly(pieces, 1, "map_ks");
                    GetCurrentDefinition().SpecularColorTexture = pieces[1];
                    break;

                case "map_bump":
                case "bump":
                    ExpectExactly(pieces, 1, "map_bump");
                    GetCurrentDefinition().BumpMap = pieces[1];
                    break;

                case "map_d":
                    ExpectExactly(pieces, 1, "map_d");
                    GetCurrentDefinition().AlphaMap = pieces[1];
                    break;

                case "map_ns":
                    ExpectExactly(pieces, 1, "map_ns");
                    GetCurrentDefinition().SpecularHighlightTexture = pieces[1];
                    break;

                default:
                    throw new ObjParseException(
                        string.Format(
                            "An unsupported line-type specifier, '{0}', was used on line {1}, \"{2}\"",
                            pieces[0],
                            _currentLine,
                            _currentLineText
                        )
                    );
            }
        }

        MaterialDefinition GetCurrentDefinition()
        {
            if (_currentDefinition == null)
                throw new InvalidDataException();

            return _currentDefinition;
        }

        void FinalizeCurrentMaterial()
        {
            if (_currentDefinition != null)
            {
                _definitions.Add(_currentDefinition);
                _currentDefinition = null;
            }
        }

        public void EndOfFileReached() => FinalizeCurrentMaterial();
        public MtlFile FinalizeFile() => new(_definitions);

        Vector3 ParseVector3(string xStr, string yStr, string zStr, string location)
        {
            try
            {
                float x = float.Parse(xStr, CultureInfo.InvariantCulture);
                float y = float.Parse(yStr, CultureInfo.InvariantCulture);
                float z = float.Parse(zStr, CultureInfo.InvariantCulture);

                return new(x, y, z);
            }
            catch (FormatException fe)
            {
                throw CreateParseException(location, fe);
            }
        }

        Vector2 ParseVector2(string xStr, string yStr, string location)
        {
            try
            {
                float x = float.Parse(xStr, CultureInfo.InvariantCulture);
                float y = float.Parse(yStr, CultureInfo.InvariantCulture);

                return new(x, y);
            }
            catch (FormatException fe)
            {
                throw CreateParseException(location, fe);
            }
        }

        int ParseInt(string intStr, string location)
        {
            try
            {
                int i = int.Parse(intStr, CultureInfo.InvariantCulture);
                return i;
            }
            catch (FormatException fe)
            {
                throw CreateParseException(location, fe);
            }
        }

        float ParseFloat(string intStr, string location)
        {
            try
            {
                float f = float.Parse(intStr, CultureInfo.InvariantCulture);
                return f;
            }
            catch (FormatException fe)
            {
                throw CreateParseException(location, fe);
            }
        }

        void ExpectExactly(string[] pieces, int count, string name)
        {
            if (pieces.Length != count + 1)
            {
                string message = string.Format(
                    "Expected exactly {0} components to a line starting with {1}, on line {2}, \"{3}\".",
                    count,
                    name,
                    _currentLine,
                    _currentLineText
                );
                throw new MtlParseException(message);
            }
        }

        void ExpectAtLeast(string[] pieces, int count, string name)
        {
            if (pieces.Length < count + 1)
            {
                string message = string.Format(
                    "Expected at least {0} components to a line starting with {1}, on line {2}, \"{3}\".",
                    count,
                    name,
                    _currentLine,
                    _currentLineText
                );
                throw new MtlParseException(message);
            }
        }

        MtlParseException CreateParseException(string location, Exception e)
        {
            string message = string.Format(
                "An error ocurred while parsing {0} on line {1}, \"{2}\"",
                location,
                _currentLine,
                _currentLineText
            );
            return new(message, e);
        }
    }
}

/// <summary>
/// A parsing error for Wavefront MTL files.
/// </summary>
public class MtlParseException : Exception
{
    /// <summary>
    /// Creates a new <see cref="MtlParseException"/>.
    /// </summary>
    public MtlParseException(string message)
        : base(message) { }

    /// <summary>
    /// Creates a new <see cref="MtlParseException"/>.
    /// </summary>
    public MtlParseException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Represents a parsed MTL definition file.
/// </summary>
public class MtlFile
{
    /// <summary>
    /// Gets a mapping of all <see cref="MaterialDefinition"/>s contained in this <see cref="MtlFile"/>.
    /// </summary>
    public IReadOnlyDictionary<string, MaterialDefinition> Definitions { get; }

    /// <summary>
    /// Constructs a new <see cref="MtlFile"/> from pre-parsed material definitions.
    /// </summary>
    /// <param name="definitions">A collection of material definitions.</param>
    public MtlFile(IEnumerable<MaterialDefinition> definitions)
    {
        Definitions = definitions.ToDictionary(def => def.Name);
    }
}

/// <summary>
/// An individual material definition from a Wavefront MTL file.
/// </summary>
public class MaterialDefinition
{
    internal MaterialDefinition(string name) => Name = name;

    /// <summary>
    /// The name of the material.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The ambient reflectivity of the material.
    /// </summary>
    public Vector3 AmbientReflectivity { get; internal set; }
    /// <summary>
    /// The diffuse reflectivity of the material.
    /// </summary>
    public Vector3 DiffuseReflectivity { get; internal set; }
    /// <summary>
    /// The specular reflectivity of the material.
    /// </summary>
    public Vector3 SpecularReflectivity { get; internal set; }
    /// <summary>
    /// The emissive coefficient of the material.
    /// </summary>
    public Vector3 EmissiveCoefficient { get; internal set; }
    /// <summary>
    /// The transmission filter of the material.
    /// </summary>
    public Vector3 TransmissionFilter { get; internal set; }
    /// <summary>
    /// The illumination model of the material.
    /// </summary>
    public int IlluminationModel { get; internal set; }
    /// <summary>
    /// The opacity of the material.
    /// </summary>
    public float Opacity { get; internal set; }
    /// <summary>
    /// The transparency of the material.
    /// </summary>
    public float Transparency => 1 - Opacity;
    /// <summary>
    /// The specular exponent of the material.
    /// </summary>
    public float SpecularExponent { get; internal set; }
    /// <summary>
    /// The sharpness of the material.
    /// </summary>
    public float Sharpness { get; internal set; }
    /// <summary>
    /// The optical density of the material.
    /// </summary>
    public float OpticalDensity { get; internal set; }

    /// <summary>
    /// The path to the ambient texture of the material.
    /// </summary>
    public string? AmbientTexture { get; internal set; }
    /// <summary>
    /// The path to the diffuse texture of the material.
    /// </summary>
    public string? DiffuseTexture { get; internal set; }
    /// <summary>
    /// The path to the specular color texture of the material.
    /// </summary>
    public string? SpecularColorTexture { get; internal set; }
    /// <summary>
    /// The path to the specular highlight texture of the material.
    /// </summary>
    public string? SpecularHighlightTexture { get; internal set; }
    /// <summary>
    /// The path to the alpha map of the material.
    /// </summary>
    public string? AlphaMap { get; internal set; }
    /// <summary>
    /// The path to the bump map of the material.
    /// </summary>
    public string? BumpMap { get; internal set; }
    /// <summary>
    /// The path to the displacement map of the material.
    /// </summary>
    public string? DisplacementMap { get; internal set; }
    /// <summary>
    /// The path to the stencil decal texture of the material.
    /// </summary>
    public string? StencilDecalTexture { get; internal set; }
}
