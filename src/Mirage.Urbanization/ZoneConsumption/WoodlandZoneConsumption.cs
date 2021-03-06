﻿using System.Drawing;
using System.Reflection;
using Mirage.Urbanization.ZoneConsumption.Base;
using Mirage.Urbanization.ZoneConsumption.Base.Behaviours;

namespace Mirage.Urbanization.ZoneConsumption
{
    public class WoodlandZoneConsumption : BaseNetworkZoneConsumption, ISingleZoneConsumptionWithPollutionBehaviour
    {
        public WoodlandZoneConsumption(ZoneInfoFinder navigator) : base(navigator) { }

        public override string Name => "Woodlands";

        public override int Cost => 25;

        public override char KeyChar => 'b';
        public override Image Tile => new Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream("Mirage.Urbanization.Tiles.woodland.png"));

        private static readonly DynamicPollutionBehaviour WoodlandPollutionBehaviour = new DynamicPollutionBehaviour(() => -50);

        public IPollutionBehaviour PollutionBehaviour => WoodlandPollutionBehaviour;

        public override Color Color => System.Drawing.Color.DarkGreen;

        public override IGetCanOverrideWithResult GetCanOverrideWith(IAreaZoneConsumption consumption)
        {
            return new AreaZoneConsumptionOverrideInfoResult(consumption, consumption);
        }
    }
}