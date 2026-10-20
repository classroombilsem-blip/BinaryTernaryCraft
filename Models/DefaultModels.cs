namespace BinaryTernaryCraft.Models
{
    public static class DefaultModels
    {
        // 1. Kinetic Shaft: Entire model rotates around [8, 8, 8]
        public const string KineticShaftGeoJson = """
        {
          "format_version": "1.12.0",
          "minecraft:geometry": [
            {
              "description": {
                "identifier": "geometry.kinetic_shaft",
                "texture_width": 64,
                "texture_height": 64
              },
              "bones": [
                {
                  "name": "shaft",
                  "pivot": [8, 8, 8],
                  "cubes": [
                    {
                      "origin": [6, 6, 0],
                      "size": [4, 4, 16],
                      "uv": [0, 0]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        // 2. Kinetic Gear: Rotating gear cog with teeth around center
        public const string KineticGearGeoJson = """
        {
          "format_version": "1.12.0",
          "minecraft:geometry": [
            {
              "description": {
                "identifier": "geometry.kinetic_gear",
                "texture_width": 64,
                "texture_height": 64
              },
              "bones": [
                {
                  "name": "gear",
                  "pivot": [8, 8, 8],
                  "cubes": [
                    {
                      "origin": [6, 6, 6],
                      "size": [4, 4, 4],
                      "uv": [0, 0]
                    },
                    {
                      "origin": [2, 2, 7],
                      "size": [12, 12, 2],
                      "uv": [16, 0]
                    },
                    {
                      "origin": [0, 6, 7],
                      "size": [16, 4, 2],
                      "uv": [0, 20]
                    },
                    {
                      "origin": [6, 0, 7],
                      "size": [4, 16, 2],
                      "uv": [20, 20]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        // 3. Electric Motor: Has static 'casing' / 'base' bone and kinetic 'rotor' bone
        public const string ElectricMotorGeoJson = """
        {
          "format_version": "1.12.0",
          "minecraft:geometry": [
            {
              "description": {
                "identifier": "geometry.electric_motor",
                "texture_width": 64,
                "texture_height": 64
              },
              "bones": [
                {
                  "name": "casing",
                  "pivot": [8, 8, 8],
                  "cubes": [
                    {
                      "origin": [2, 2, 0],
                      "size": [12, 12, 12],
                      "uv": [0, 0]
                    },
                    {
                      "origin": [1, 1, 0],
                      "size": [14, 2, 14],
                      "uv": [0, 24]
                    }
                  ]
                },
                {
                  "name": "rotor",
                  "pivot": [8, 8, 8],
                  "cubes": [
                    {
                      "origin": [6, 6, 12],
                      "size": [4, 4, 4],
                      "uv": [0, 40]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        // 4. Ternary Piston: Has static 'base' bone and movable 'head' bone
        public const string TernaryPistonGeoJson = """
        {
          "format_version": "1.12.0",
          "minecraft:geometry": [
            {
              "description": {
                "identifier": "geometry.ternary_piston",
                "texture_width": 64,
                "texture_height": 64
              },
              "bones": [
                {
                  "name": "base",
                  "pivot": [8, 8, 8],
                  "cubes": [
                    {
                      "origin": [0, 0, 0],
                      "size": [16, 4, 16],
                      "uv": [0, 0]
                    }
                  ]
                },
                {
                  "name": "head",
                  "pivot": [8, 4, 8],
                  "cubes": [
                    {
                      "origin": [6, 4, 6],
                      "size": [4, 10, 4],
                      "uv": [0, 20]
                    },
                    {
                      "origin": [0, 12, 0],
                      "size": [16, 4, 16],
                      "uv": [16, 0]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;
    }
}
