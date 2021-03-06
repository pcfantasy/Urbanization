using System;
using Mirage.Urbanization.Simulation.Persistence;

namespace Mirage.Urbanization.Simulation
{
    public interface ICityBudget
    {
        int CurrentAmount { get; }

        string CurrentAmountDescription { get; }

        int ProjectedIncome { get; }

        string ProjectedIncomeDescription { get; }
        PersistedCityStatisticsWithFinancialData ProcessFinances(PersistedCityStatistics persistedCityStatistics, ICityBudgetConfiguration cityBudget);
        void AddProjectedIncomeToCurrentAmount();
        event EventHandler<CityBudgetValueChangedEventArgs> OnCityBudgetValueChanged;
        void RaiseCityBudgetValueChangedEvent();

        void RestoreFrom(PersistedCityStatisticsWithFinancialData persistedCityStatisticsWithFinancialData);

        void Handle(IAreaConsumptionResult areaConsumptionResult);
    }
}