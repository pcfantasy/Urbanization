using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mirage.Urbanization.ZoneConsumption;
using Mirage.Urbanization.ZoneConsumption.Base;
using Mirage.Urbanization.ZoneConsumption.Base.Behaviours;

namespace Mirage.Urbanization
{
    [DebuggerDisplay("Zone info: (X: {Point.X}, Y: {Point.Y})")]
    public class ZoneInfo : IZoneInfo
    {
        private readonly IZoneConsumptionState _zoneState;
        private readonly ZonePoint _zonePoint;
        private readonly GetRelativeZoneInfoDelegate _getRelativeZoneInfo;
        private readonly ILandValueCalculator _landValueCalculator;
        private readonly GrowthAlgorithmHighlightState _highlightState = new GrowthAlgorithmHighlightState();

        public IGrowthAlgorithmHighlightState GrowthAlgorithmHighlightState { get { return _highlightState; } }

        public IZoneConsumptionState ConsumptionState { get { return _zoneState; } }
        public ZonePoint Point { get { return _zonePoint; } }

        public ZoneInfo(
            ZonePoint zonePoint,
            GetRelativeZoneInfoDelegate getRelativeZoneInfo,
            ILandValueCalculator landValueCalculator
            )
        {
            if (zonePoint == null) throw new ArgumentNullException("zonePoint");
            if (getRelativeZoneInfo == null) throw new ArgumentNullException("getRelativeZoneInfo");
            if (landValueCalculator == null) throw new ArgumentNullException("landValueCalculator");

            _zoneState = new ZoneConsumptionState();
            _zonePoint = zonePoint;
            _getRelativeZoneInfo = getRelativeZoneInfo;
            _landValueCalculator = landValueCalculator;
        }

        public QueryResult<IZoneInfo, RelativeZoneInfoQuery> GetRelativeZoneInfo(RelativeZoneInfoQuery relativeZoneInfoQuery)
        {
            return _getRelativeZoneInfo(relativeZoneInfoQuery);
        }

        public int GetDistanceScoreBasedOnConsumption()
        {
            return _zoneState.GetIsRailroadNetworkMember() || _zoneState.GetIsPowerGridMember()
                ? 1
                : 3;

        }

        private QueryResult<IZoneInfo, RelativeZoneInfoQuery> _north;
        public QueryResult<IZoneInfo, RelativeZoneInfoQuery> GetNorth()
        {
            return _north ?? (_north = _getRelativeZoneInfo(
                new RelativeZoneInfoQuery(relativeX: 0, relativeY: -1)
                ));
        }

        private QueryResult<IZoneInfo, RelativeZoneInfoQuery> _south;
        public QueryResult<IZoneInfo, RelativeZoneInfoQuery> GetSouth()
        {
            return _south ?? (_south = _getRelativeZoneInfo(
                new RelativeZoneInfoQuery(relativeX: 0, relativeY: 1)
                ));
        }

        private QueryResult<IZoneInfo, RelativeZoneInfoQuery> _east;
        public QueryResult<IZoneInfo, RelativeZoneInfoQuery> GetEast()
        {
            return _east ?? (_east = _getRelativeZoneInfo(
                new RelativeZoneInfoQuery(relativeX: 1, relativeY: 0)
                ));
        }

        private QueryResult<IZoneInfo, RelativeZoneInfoQuery> _west;
        public QueryResult<IZoneInfo, RelativeZoneInfoQuery> GetWest()
        {
            return _west ?? (_west = _getRelativeZoneInfo(
                new RelativeZoneInfoQuery(relativeX: -1, relativeY: 0)
                ));
        }

        public IEnumerable<QueryResult<IZoneInfo, RelativeZoneInfoQuery>> GetNorthEastSouthWest()
        {
            yield return GetNorth();
            yield return GetEast();
            yield return GetSouth();
            yield return GetWest();
        }

        public IEnumerable<IZoneInfo> CrawlAllDirections(Func<IZoneInfo, bool> predicate)
        {
            return CrawlAllDirections(predicate, new HashSet<IZoneInfo>());
        }

        private IEnumerable<IZoneInfo> CrawlAllDirections(Func<IZoneInfo, bool> predicate, HashSet<IZoneInfo> samples)
        {
            foreach (var match in GetNorthEastSouthWest()
                .Where(x => x.HasMatch && predicate(x.MatchingObject))
                .Where(x => samples.Add(x.MatchingObject)))
            {
                foreach (var subEntry in (match.MatchingObject as ZoneInfo).CrawlAllDirections(predicate, samples))
                    samples.Add(subEntry);
            }
            return samples;
        }

        private readonly IDictionary<int, ISet<QueryResult<IZoneInfo, RelativeZoneInfoQuery>>> _diamondCache =
            new Dictionary<int, ISet<QueryResult<IZoneInfo, RelativeZoneInfoQuery>>>();

        private readonly object _diamondCacheLock = new object();

        public IEnumerable<QueryResult<IZoneInfo, RelativeZoneInfoQuery>> GetSurroundingZoneInfosDiamond(int size)
        {
            lock (_diamondCacheLock)
            {
                if (!_diamondCache.ContainsKey(size))
                {
                    _diamondCache.Add(size, new HashSet<QueryResult<IZoneInfo, RelativeZoneInfoQuery>>());
                    for (int y = -size; y <= size; y++)
                    {
                        for (int x = -size; x <= size; x++)
                        {
                            if (Math.Abs(x) + Math.Abs(y) <= size)
                            {
                                _diamondCache[size].Add(_getRelativeZoneInfo(new RelativeZoneInfoQuery(x, y)));
                            }
                        }
                    }
                }
                return _diamondCache[size];
            }
        }

        IReadOnlyZoneConsumptionState IReadOnlyZoneInfo.ZoneConsumptionState
        {
            get { return _zoneState; }
        }

        public QueryResult<IQueryPollutionResult> QueryPollution()
        {
            int pollution = (
                from match in GetSurroundingZoneInfosDiamond(4)
                    .Where(x => x.HasMatch)
                let pollutionBehaviourResult = match
                    .MatchingObject
                    .GetPollutionBehaviour()
                where pollutionBehaviourResult.HasMatch
                select pollutionBehaviourResult
                    .MatchingObject
                    .GetPollutionInUnits(match.QueryObject)
                ).Sum();

            _lastQueryPollutionResult = new QueryResult<IQueryPollutionResult>(pollution > 0 ? new QueryPollutionResult(pollution) : null);
            return _lastQueryPollutionResult;
        }

        private QueryResult<IQueryPollutionResult> _lastQueryPollutionResult;
        public QueryResult<IQueryPollutionResult> GetLastQueryPollutionResult()
        {
            return _lastQueryPollutionResult ?? QueryPollution();
        }

        public QueryResult<IPollutionBehaviour> GetPollutionBehaviour()
        {
            var consumptionState = ConsumptionState.GetZoneConsumption();
            IPollutionBehaviour pollutionBehaviour = null;

            if (consumptionState is ZoneClusterMemberConsumption)
            {
                pollutionBehaviour = (consumptionState as ZoneClusterMemberConsumption).ParentBaseZoneClusterConsumption
                    .PollutionBehaviour;
            }
            else if (consumptionState is WoodlandZoneConsumption)
            {
                pollutionBehaviour = (consumptionState as WoodlandZoneConsumption).PollutionBehaviour;
            }
            else
            {
                ConsumptionState.WithNetworkMember<RoadZoneConsumption>(roadZoneConsumption => pollutionBehaviour = roadZoneConsumption.PollutionBehaviour);
            }

            return new QueryResult<IPollutionBehaviour>(pollutionBehaviour);
        }

        public QueryResult<ICrimeBehaviour> GetCrimeBehaviour()
        {
            var consumptionState = ConsumptionState.GetZoneConsumption();
            ICrimeBehaviour crimeBehaviour = null;

            if (consumptionState is ZoneClusterMemberConsumption)
            {
                crimeBehaviour =
                    (consumptionState as ZoneClusterMemberConsumption).ParentBaseZoneClusterConsumption.CrimeBehaviour;
            }
            return new QueryResult<ICrimeBehaviour>(crimeBehaviour);
        }

        private QueryResult<IQueryCrimeResult> _lastQueryCrimeResult;
        public QueryResult<IQueryCrimeResult> QueryCrime()
        {
            if (!this.ConsumptionState.GetIsZoneClusterMember())
            {
                return _lastQueryCrimeResult = new QueryResult<IQueryCrimeResult>();
            }

            int crimeInUnits = (from match in GetSurroundingZoneInfosDiamond(20)
                .Where(x => x.HasMatch)
                let crimeBehaviourResult = match
                    .MatchingObject
                    .GetCrimeBehaviour()
                where crimeBehaviourResult.HasMatch
                select crimeBehaviourResult
                    .MatchingObject
                    .GetCrimeInUnits(match.QueryObject)
                ).Sum();

            return _lastQueryCrimeResult = new QueryResult<IQueryCrimeResult>(new QueryCrimeResult(crimeInUnits));
        }

        public QueryResult<IQueryCrimeResult> GetLastQueryCrimeResult()
        {
            return _lastQueryCrimeResult ?? QueryCrime();
        }

        public QueryResult<IFireHazardBehaviour> GetFireHazardBehaviour()
        {
            var consumptionState = ConsumptionState.GetZoneConsumption();
            IFireHazardBehaviour FireHazardBehaviour = null;

            if (consumptionState is ZoneClusterMemberConsumption)
            {
                FireHazardBehaviour =
                    (consumptionState as ZoneClusterMemberConsumption).ParentBaseZoneClusterConsumption.FireHazardBehaviour;
            }
            return new QueryResult<IFireHazardBehaviour>(FireHazardBehaviour);
        }

        private QueryResult<IQueryFireHazardResult> _lastQueryFireHazardResult;
        public QueryResult<IQueryFireHazardResult> QueryFireHazard()
        {
            if (!this.ConsumptionState.GetIsZoneClusterMember())
            {
                return _lastQueryFireHazardResult = new QueryResult<IQueryFireHazardResult>();
            }

            int FireHazardInUnits = (from match in GetSurroundingZoneInfosDiamond(20)
                .Where(x => x.HasMatch)
                let FireHazardBehaviourResult = match
                    .MatchingObject
                    .GetFireHazardBehaviour()
                where FireHazardBehaviourResult.HasMatch
                select FireHazardBehaviourResult
                    .MatchingObject
                    .GetFireHazardInUnits(match.QueryObject)
                ).Sum();

            return _lastQueryFireHazardResult = new QueryResult<IQueryFireHazardResult>(new QueryFireHazardResult(FireHazardInUnits));
        }

        public QueryResult<IQueryFireHazardResult> GetLastQueryFireHazardResult()
        {
            return _lastQueryFireHazardResult ?? QueryFireHazard();
        }

        public void WithZoneConsumptionIf<T>(Action<T> action) where T : BaseZoneConsumption
        {
            var consumption = this.ConsumptionState.GetZoneConsumption() as T;
            if (consumption != null)
                action(consumption);
        }

        public QueryResult<TNetworkZoneConsumption> GetNetworkZoneConsumption<TNetworkZoneConsumption>()
            where TNetworkZoneConsumption : BaseNetworkZoneConsumption
        {
            var consumption = ConsumptionState.GetZoneConsumption();

            if (consumption is TNetworkZoneConsumption)
                return new QueryResult<TNetworkZoneConsumption>(consumption as TNetworkZoneConsumption);

            if (consumption is IntersectingZoneConsumption)
            {
                return new QueryResult<TNetworkZoneConsumption>((consumption as IntersectingZoneConsumption)
                    .GetIntersectingZoneConsumptions()
                    .OfType<TNetworkZoneConsumption>()
                    .SingleOrDefault()
                    );
            }
            return new QueryResult<TNetworkZoneConsumption>();
        }

        public void WithNetworkConsumptionIf<TNetworkZoneConsumption>(Action<TNetworkZoneConsumption> action)
            where TNetworkZoneConsumption : BaseNetworkZoneConsumption
        {
            GetNetworkZoneConsumption<TNetworkZoneConsumption>()
                .WithResultIfHasMatch(action);
        }

        public bool IsGrowthZoneClusterOfType<T>() where T : BaseGrowthZoneClusterConsumption
        {
            var match = false;
            WithZoneClusterIf<T>(t => match = t.CanGrowAndHasPower);
            return match;
        }

        public QueryResult<T> GetAsZoneCluster<T>() where T : BaseZoneClusterConsumption
        {
            T value = null;

            WithZoneClusterIf<T>(x => value = x);
            return new QueryResult<T>(value);
        }

        public void WithZoneClusterIf<T>(Action<T> action) where T : BaseZoneClusterConsumption
        {
            WithZoneConsumptionIf<ZoneClusterMemberConsumption>(x =>
            {
                var parent = x.ParentBaseZoneClusterConsumption as T;
                if (parent != null)
                    action(parent);
            });
        }

        public int? GetLastAverageTravelDistance()
        {
            var returnValue = default(int?);
            WithZoneClusterIf<BaseGrowthZoneClusterConsumption>(cluster =>
            {
                returnValue = cluster.AverageTravelDistance;
            });
            return returnValue;
        }


        public int GetPopulationDensity()
        {
            var returnValue = default(int);
            WithZoneClusterIf<BaseGrowthZoneClusterConsumption>(cluster =>
            {
                returnValue = cluster.PopulationDensity;
            });
            return returnValue;
        }

        public QueryResult<IQueryLandValueResult> QueryLandValue()
        {
            return _lastQueryLandValueResult = _landValueCalculator.GetFor(this);
        }

        private QueryResult<IQueryLandValueResult> _lastQueryLandValueResult;

        public QueryResult<IQueryLandValueResult> GetLastLandValueResult()
        {
            return _lastQueryLandValueResult ?? QueryLandValue();
        }

        public IEnumerable<Func<IZoneInfo, QueryResult<IZoneInfo, RelativeZoneInfoQuery>>> GetSteerDirections(Func<IZoneInfo, QueryResult<IZoneInfo, RelativeZoneInfoQuery>> expression)
        {
            var result = expression(this);

            if (result == GetNorth() || result == GetSouth())
            {
                yield return x => x.GetEast();
                yield return x => x.GetWest();
            }
            else if (result == GetEast() || result == GetWest())
            {
                yield return x => x.GetNorth();
                yield return x => x.GetSouth();
            }
            else
                throw new InvalidOperationException();
        }
    }
}