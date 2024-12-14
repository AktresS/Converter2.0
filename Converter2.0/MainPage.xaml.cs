using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Converter2._0 {
    public partial class MainPage : ContentPage {
        public MainPage() {
            InitializeComponent();
            DateSelector.MaximumDate = DateTime.Today;
           
        }
    }

    internal class ConverterViewModel : INotifyPropertyChanged {
        private readonly ApiService _api;
        private CurrencyRates _currencyRates;
        private Dictionary<string, Currency> _currencies;

        public ObservableCollection<string> Currencies { get; private set; }

        private bool _isAvailable = false;
        public bool IsAvailable {
            get => !_isAvailable;
            set {
                _isAvailable = value;
                OnPropertyChanged();
            }
        }

        private DateTime _selectedDate = DateTime.Now;
        public DateTime SelectedDate {
            get => _selectedDate;
            set {
                if (_selectedDate != value) {
                    _selectedDate = value;
                    OnPropertyChanged();
                    LoadCurrencies();
                }
            }
        }

        private string _selectedFromCurrency = "";
        public string SelectedFromCurrency {
            get => _selectedFromCurrency;
            set {
                if (_selectedFromCurrency != value) {
                    _selectedFromCurrency = value;
                    OnPropertyChanged();
                    ConvertCurrencies();
                }
            }
        }

        private string _selectedToCurrency = "";
        public string SelectedToCurrency {
            get => _selectedToCurrency;
            set {
                if (_selectedToCurrency != value) {
                    _selectedToCurrency = value;
                    OnPropertyChanged();
                    ConvertCurrencies();
                }
            }
        }

        private string _inputAmount = "1.00";
        public string InputAmount {
            get => _inputAmount;
            set {
                if (_inputAmount != value)
                {
                    _inputAmount = value;
                    OnPropertyChanged();
                    ConvertCurrencies();
                }
            }
        }

        private string _convertedAmount = "1.00";
        public string ConvertedAmount {
            get => _convertedAmount;
            set {
                if (_convertedAmount != value) {
                    _convertedAmount = value;
                    OnPropertyChanged();
                    ConvertCurrencies();
                }
            }
        }

        public ConverterViewModel() {
            _api = new ApiService();
            Currencies = new ObservableCollection<string>();
            LoadCurrencies();
        }

        private async Task LoadCurrencies() {

            try {
                IsAvailable = true;

                var previousFromCurrency = SelectedFromCurrency;
                var previousToCurrency = SelectedToCurrency;

                _currencyRates = await _api.GetCurrencyRatesAsync(_selectedDate);
                _currencies = _currencyRates.Valute;
                Currencies.Clear();

                foreach (var cur in _currencies) {
                    Currencies.Add($"{cur.Value.Name}, ({cur.Key})");
                }


                SelectedFromCurrency = Currencies.Contains(previousFromCurrency) ? previousFromCurrency : 
                    Currencies[0];

                SelectedToCurrency = Currencies.Contains(previousToCurrency) ? previousToCurrency : Currencies[0];

                SelectedDate = _currencyRates.Date;
                IsAvailable = false;
                ConvertCurrencies();
            } catch (Exception ex) {
                Console.WriteLine($"Error loading {ex.Message}");
            }
        }

        private void ConvertCurrencies() {

            if (double.TryParse(InputAmount, CultureInfo.InvariantCulture, out double inputAmountDouble) && !string.IsNullOrEmpty(SelectedFromCurrency) && 
                !string.IsNullOrEmpty(SelectedToCurrency) && !string.IsNullOrEmpty(InputAmount) && _currencies != null) {

                string fromCurrencySplit = SelectedFromCurrency.Split('(', ')')[^2];
                string toCurrencySplit = SelectedToCurrency.Split('(', ')')[^2];

                if (_currencies.ContainsKey(fromCurrencySplit) && _currencies.ContainsKey(toCurrencySplit)) {
                    Currency fromCurrency = _currencies[fromCurrencySplit];
                    Currency toCurrency = _currencies[toCurrencySplit];

                    double convertedAmount = ApiService.ConvertCurrencies(fromCurrency, toCurrency, inputAmountDouble);
                    ConvertedAmount = convertedAmount.ToString("F2");
                }
                else {
                    ConvertedAmount = string.Empty;
                }
            }
            else {
                ConvertedAmount = string.Empty;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "") {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    public class ApiService {

        private readonly HttpClient _client;

        public ApiService() {
            _client = new HttpClient {
                BaseAddress = new Uri("https://www.cbr-xml-daily.ru/")
            };
        }

        public async Task<CurrencyRates> GetCurrencyRatesAsync(DateTime date) {

            string resource = "daily_json.js";

            if (date != DateTime.Today) {
                string year = date.Year.ToString();
                string month = date.Month.ToString("00");
                string day = date.Day.ToString("00");

                resource = $"/archive//{year}//{month}//{day}//daily_json.js";
            }

            try {
                HttpResponseMessage response = await _client.GetAsync(resource);

                if (response.IsSuccessStatusCode) {
                    var json = await response.Content.ReadAsStringAsync();
                    var currencyRates = JsonSerializer.Deserialize<CurrencyRates>(json);
                    currencyRates.Valute.Add("RUB", new Currency {
                        CharCode = "RUB",
                        Name = "Российский рубль",
                        Nominal = 1,
                        Value = 1
                    });
                    return currencyRates;
                }
                else {
                    return await GetCurrencyRatesAsync(date.AddDays(-1));
                }
                

                
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
                return null;
            }
            
        }

        public static double ConvertCurrencies(Currency fromCurrency,  Currency toCurrency, double inputAmount) {

            if (fromCurrency != null && toCurrency != null && inputAmount != 0) {
                double fromCurrencyRate = fromCurrency.Value / fromCurrency.Nominal;
                double toCurrencyRate = toCurrency.Value / toCurrency.Nominal;

                double converted = fromCurrencyRate / toCurrencyRate;

                double convertedAmount = converted * inputAmount;

                return convertedAmount;
            }
            else {
                return 0;
            }
        } 
    }

    public class CurrencyRates {
        public DateTime Date { get; set; }
        public DateTime PreviousDate { get; set; }
        public string PreviousURL { get; set; } = "";
        public DateTime Timestamp { get; set; }

        public Dictionary<string, Currency> Valute { get; set; } = new Dictionary<string, Currency>();
    }

    public class Currency {
        public string ID { get; set; }
        public string NumCode { get; set; }
        public string CharCode { get; set; }
        public int Nominal { get; set; }
        public string Name { get; set; }
        public double Value { get; set; }
        public double Previous { get; set; }
    }
}
