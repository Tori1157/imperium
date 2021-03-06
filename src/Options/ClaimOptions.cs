﻿namespace Oxide.Plugins
{
  using Newtonsoft.Json;
  using System.Collections.Generic;

  public partial class Imperium : RustPlugin
  {
    class ClaimOptions
    {
      [JsonProperty("enabled")]
      public bool Enabled;

      [JsonProperty("costs")]
      public List<int> Costs = new List<int>();

      [JsonProperty("maxClaims")]
      public int? MaxClaims;

      [JsonProperty("minAreaNameLength")]
      public int MinAreaNameLength;

      [JsonProperty("maxAreaNameLength")]
      public int MaxAreaNameLength;

      [JsonProperty("minFactionMembers")]
      public int MinFactionMembers;

      [JsonProperty("requireContiguousClaims")]
      public bool RequireContiguousClaims;

      public static ClaimOptions Default = new ClaimOptions {
        Enabled = true,
        Costs = new List<int> { 0, 100, 200, 300, 400, 500 },
        MaxClaims = null,
        MinAreaNameLength = 3,
        MaxAreaNameLength = 20,
        MinFactionMembers = 3,
        RequireContiguousClaims = false
      };
    }
  }
}
