﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RawBencher.Benchers
{
	/// <summary>
	/// Abstract base class for the benchers. Each bencher inherits from this class to be used in the bench run.
	/// </summary>
	/// <typeparam name="T">The type of the element fetched by the framework this bencher is for, e.g. an entity class type</typeparam>
	public abstract class BencherBase<T> : IBencher
	{
		#region Member declarations
		private Func<T, int> _salesOrderIdRetriever;
		private List<BenchResult> _individualBenchmarkResults, _setBenchmarkResults, _eagerLoadBenchmarkResults, _asyncEagerLoadBenchmarkResults;
		private string _frameworkName;
		#endregion


		/// <summary>
		/// Initializes a new instance of the <see cref="BencherBase{T}" /> class.
		/// </summary>
		/// <param name="salesOrderIdRetriever">The salesOrderId retriever func, to be used in the data verification step.</param>
		/// <param name="usesChangeTracking">if set to <c>true</c> the fetches will be resulting in change tracked elements</param>
		/// <param name="usesCaching">if set to <c>true</c> the fetches will be using some form of caching (resultset caching, element caching)</param>
		/// <param name="supportsEagerLoading">if set to <c>true</c> this bencher supports eager loading and will participate in eager loading benchmarks.</param>
		/// <param name="supportsAsync">if set to <c>true</c> this bencher supports async and will participate in async benchmarks.</param>
		/// <exception cref="ArgumentNullException">salesOrderIdRetriever</exception>
		/// <exception cref="System.ArgumentNullException">salesOrderIdRetriever</exception>
		protected BencherBase(Func<T, int> salesOrderIdRetriever, bool usesChangeTracking, bool usesCaching, bool supportsEagerLoading=false, bool supportsAsync=false)
		{
			if(salesOrderIdRetriever == null)
			{
				throw new ArgumentNullException("salesOrderIdRetriever");
			}
			_salesOrderIdRetriever = salesOrderIdRetriever;
			this.UsesCaching = usesCaching;
			this.UsesChangeTracking = usesChangeTracking;
			this.SupportsEagerLoading = supportsEagerLoading;
			this.SupportsAsync = supportsAsync;
			_individualBenchmarkResults = new List<BenchResult>();
			_setBenchmarkResults = new List<BenchResult>();
			_eagerLoadBenchmarkResults = new List<BenchResult>();
			_asyncEagerLoadBenchmarkResults = new List<BenchResult>();
			_frameworkName = string.Empty;
		}


		/// <summary>
		/// Creates the name of the framework this bencher is for. Use the overload which accepts a format string and a type to create a name based on a
		/// specific version
		/// </summary>
		/// <returns>the framework name.</returns>
		public string CreateFrameworkName()
		{
			if(string.IsNullOrEmpty(_frameworkName))
			{
				_frameworkName = CreateFrameworkNameImpl();
			}
			return _frameworkName;
		}


		/// <summary>
		/// Resets the result containers of this bencher.
		/// </summary>
		public void ResetResults()
		{
			_individualBenchmarkResults.Clear();
			_setBenchmarkResults.Clear();
			_eagerLoadBenchmarkResults.Clear();
			_asyncEagerLoadBenchmarkResults.Clear();
			this.SetFetchMean = 0.0;
			this.SetFetchSD = 0.0;
			this.IndividualFetchMean = 0.0;
			this.IndividualFetchSD = 0.0;
			this.EnumerationMean = 0.0;
			this.EnumerationSD = 0.0;
			this.EagerLoadFetchMean = 0.0;
			this.EagerLoadFetchSD = 0.0;
			this.AsyncEagerLoadFetchMean = 0.0;
			this.AsyncEagerLoadFetchSD = 0.0;
		}


		/// <summary>
		/// Calculates the mean and standard deviation values from the results obtained through the benchmark methods. Requires at least 3 runs of the benchmark methods to produce
		/// valid results. Results are obtainable through the properties <see cref="IndividualFetchMean"/>, <see cref="IndividualFetchSD"/>, <see cref="SetFetchMean"/>, 
		/// <see cref="SetFetchSD"/>, <see cref="EnumerationMean"/>, <see cref="EnumerationSD"/>, <see cref="EagerLoadFetchMean"/>, <see cref="EagerLoadFetchSD"/>, 
		/// <see cref="AsyncEagerLoadFetchSD"/>, <see cref="AsyncEagerLoadFetchMean"/>.
		/// </summary>
		public void CalculateStatistics()
		{
			var individualFetchValues = _individualBenchmarkResults.Select(r => r.FetchTimeInMilliseconds).ToList();
			this.IndividualFetchMean = individualFetchValues.Count <= 0 ? -1.0 : individualFetchValues.Average();
			this.IndividualFetchSD = CalculateSD(individualFetchValues, this.IndividualFetchMean);

			var setFetchValues = _setBenchmarkResults.Select(r => r.FetchTimeInMilliseconds).ToList();
			this.SetFetchMean = setFetchValues.Count <= 0 ? -1.0 : setFetchValues.Average();
			this.SetFetchSD = CalculateSD(setFetchValues, this.SetFetchMean);

			var enumerationValues = _setBenchmarkResults.Select(r => r.EnumerationTimeInMilliseconds).ToList();
			this.EnumerationMean = enumerationValues.Count <= 0 ? -1.0 : enumerationValues.Average();
			this.EnumerationSD = CalculateSD(enumerationValues, this.EnumerationMean);

			if(this.SupportsEagerLoading)
			{
				var eagerLoadFetchValues = _eagerLoadBenchmarkResults.Select(r=>r.FetchTimeInMilliseconds).ToList();
				this.EagerLoadFetchMean = eagerLoadFetchValues.Count <= 0 ? -1.0 : eagerLoadFetchValues.Average();
				this.EagerLoadFetchSD = CalculateSD(eagerLoadFetchValues, this.EagerLoadFetchMean);
				if(this.SupportsAsync)
				{
					var asyncEagerLoadFetchValues = _asyncEagerLoadBenchmarkResults.Select(r => r.FetchTimeInMilliseconds).ToList();
					this.AsyncEagerLoadFetchMean = asyncEagerLoadFetchValues.Count <= 0 ? -1.0 : asyncEagerLoadFetchValues.Average();
					this.AsyncEagerLoadFetchSD = CalculateSD(asyncEagerLoadFetchValues, this.AsyncEagerLoadFetchMean);
				}
			}
		}


		/// <summary>
		/// Performs the individual bench mark. This is a benchmark which fetches all SalesOrderHeader elements with the keys specified, individually.
		/// </summary>
		/// <param name="keys">The keys for all elements to fetch.</param>
		/// <param name="discardResults">if set to <c>true</c> the results are returned but are not collected.</param>
		/// <returns>
		/// A filled in benchmark result object, with the total time taken to fetch all elements of the keys specified. EnumerationTime is not set. Number of
		/// </returns>
		public BenchResult PerformIndividualBenchMark(List<int> keys, bool discardResults)
		{
			var toReturn = new BenchResult();
			int numberOfElementsFetched = 0;
			var sw = new Stopwatch();
			sw.Start();
			foreach(var key in keys)
			{
				var element = FetchIndividual(key);
				var verifyResult = VerifyElement(element);
				if(verifyResult > 0)
				{
					numberOfElementsFetched++;
				}
			}
			sw.Stop();
			toReturn.FetchTimeInMilliseconds = sw.Elapsed.TotalMilliseconds;
			toReturn.IncNumberOfRowsForType(typeof(T), numberOfElementsFetched);
			if (!discardResults)
			{
				_individualBenchmarkResults.Add(toReturn);
			}
			return toReturn;
		}


		/// <summary>
		/// Performs the individual bench mark. This is a benchmark which fetches all SalesOrderHeader elements with the keys specified, individually.
		/// </summary>
		/// <param name="keys">The keys for all elements to fetch.</param>
		/// <returns>
		/// A filled in benchmark result object, with the total time taken to fetch all elements of the keys specified. EnumerationTime is not set. Number of
		/// </returns>
		public BenchResult PerformIndividualBenchMark(List<int> keys)
		{
			return PerformIndividualBenchMark(keys, discardResults: false);
		}


		/// <summary>
		/// Performs the set benchmark. This is a benchmark which fetches the full set of sales order headers, enumerates it and returns the times it took
		/// to perform these actions as well as the number of rows read.
		/// </summary>
		/// <returns>A filled in benchmark result object</returns>
		public BenchResult PerformSetBenchmark()
		{
			return PerformSetBenchmark(discardResults: false);
		}


		/// <summary>
		/// Performs the set benchmark. This is a benchmark which fetches the full set of sales order headers, enumerates it and returns the times it took
		/// to perform these actions as well as the number of rows read.
		/// </summary>
		/// <param name="discardResults">if set to <c>true</c> the results are returned but are not collected.</param>
		/// <returns>
		/// A filled in benchmark result object
		/// </returns>
		public BenchResult PerformSetBenchmark(bool discardResults)
		{
			var toReturn = new BenchResult();
			var sw = new Stopwatch();
			sw.Start();
			var headers = FetchSet();
			sw.Stop();
			toReturn.FetchTimeInMilliseconds = sw.Elapsed.TotalMilliseconds;
			sw.Reset();
			sw.Start();
			toReturn.IncNumberOfRowsForType(typeof(T), VerifyData(headers));
			sw.Stop();
			toReturn.EnumerationTimeInMilliseconds = sw.Elapsed.TotalMilliseconds;
			if(!discardResults)
			{
				_setBenchmarkResults.Add(toReturn);
			}
			return toReturn;
		}


		/// <summary>
		/// Performs the eager load benchmark. This is a benchmark which will fetch a 2-edge graph of 1000 sales order headers, all related customer entities and all related 
		/// sales order detail entities. 
		/// </summary>
		/// <returns>
		/// A filled in benchmark result object
		/// </returns>
		public BenchResult PerformEagerLoadBenchmark()
		{
			return PerformEagerLoadBenchmark(discardResults: false);
		}


		/// <summary>
		/// Performs the eager load benchmark. This is a benchmark which will fetch a 2-edge graph of 1000 sales order headers, all related customer entities and all related 
		/// sales order detail entities. 
		/// </summary>
		/// <param name="discardResults">if set to <c>true</c> the results are returned but are not collected.</param>
		/// <returns>
		/// A filled in benchmark result object
		/// </returns>
		public BenchResult PerformEagerLoadBenchmark(bool discardResults)
		{
			var toReturn = new BenchResult();
			var sw = new Stopwatch();
			sw.Start();
			var headers = FetchGraph();
			sw.Stop();
			toReturn.FetchTimeInMilliseconds = sw.Elapsed.TotalMilliseconds;
			sw.Reset();
			sw.Start();
			VerifyGraph(headers, toReturn);
			sw.Stop();
			if(!discardResults)
			{
				_eagerLoadBenchmarkResults.Add(toReturn);
			}
			return toReturn;
		}


		/// <summary>
		/// Performs the eager load benchmark, asynchronously. This is a benchmark which will fetch a 2-edge graph of 1000 sales order headers, all related customer entities and all related 
		/// sales order detail entities. It will use a Task.Run to perform the async code. 
		/// </summary>
		/// <param name="discardResults">if set to <c>true</c> the results are returned but are not collected.</param>
		/// <returns>
		/// A filled in benchmark result object
		/// </returns>
		public BenchResult PerformAsyncEagerLoadBenchmark(bool discardResults)
		{
			var toReturn = new BenchResult();
			var sw = new Stopwatch();
			sw.Start();
			IEnumerable<T> headers = null;
			Task.Run(async ()=>headers = await FetchGraphAsync()).GetAwaiter().GetResult();
			sw.Stop();
			toReturn.FetchTimeInMilliseconds = sw.Elapsed.TotalMilliseconds;
			sw.Reset();
			sw.Start();
			VerifyGraph(headers, toReturn);
			sw.Stop();
			if(!discardResults)
			{
				_asyncEagerLoadBenchmarkResults.Add(toReturn);
			}
			return toReturn;
		}
		

		/// <summary>
		/// Fetches the individual element
		/// </summary>
		/// <typeparam name="T">Type of the element to fetch</typeparam>
		/// <param name="key">The key of the element to fetch.</param>
		/// <returns>The fetched element, or null if not found</returns>
		public abstract T FetchIndividual(int key);
		/// <summary>
		/// Fetches the complete set of elements and returns this set as an IEnumerable.
		/// </summary>
		/// <returns>
		/// the set fetched
		/// </returns>
		public abstract IEnumerable<T> FetchSet();

		/// <summary>
		/// Fetches the complete graph using eager loading and returns this as an IEnumerable.
		/// </summary>
		/// <returns>the graph fetched</returns>
		public virtual IEnumerable<T> FetchGraph()
		{
			return new List<T>();
		}

#pragma warning disable 1998		// see comment below
		/// <summary>
		/// Async variant of FetchGraph(). Fetches the complete graph using eager loading and returns this as an IEnumerable.
		/// </summary>
		/// <returns>the graph fetched</returns>
		/// <remarks>This base method will give a CS1998 warning. That's ok, it's empty anyway.</remarks>
		public async virtual Task<IEnumerable<T>> FetchGraphAsync()
		{
			return new List<T>();
		}
#pragma warning restore 1998

		/// <summary>
		/// Verifies the graph element's children. The parent should contain 2 sets of related elements: SalesOrderDetail and Customer. Both have to be counted and
		/// the count has to stored in the resultContainer, under the particular type. Implementers have to check whether the related elements are actually materialized objects.
		/// </summary>
		/// <param name="parent">The parent.</param>
		/// <param name="resultContainer">The result container.</param>
		public virtual void VerifyGraphElementChildren(T parent, BenchResult resultContainer)
		{
			// nop
		}


		/// <summary>
		/// Implements the CreateFrameworkName method. Done this way so caching of the name is performed in the caller and overrides have no influence
		/// on this.
		/// </summary>
		/// <returns>the framework name.</returns>
		protected abstract string CreateFrameworkNameImpl();


		/// <summary>
		/// Creates the name of the framework this bencher is for. Use it in the implementation of CreateFrameworkName
		/// </summary>
		/// <param name="formatString">The format string to use. Expects {0} to be for assembly version, {1} for file version.</param>
		/// <param name="t">The type in the assembly to use for file/assembly version.</param>
		/// <returns>ready to use framework name</returns>
		protected string CreateFrameworkName(string formatString, Type t)
		{
			string assemblyVersion;
			string fileVersion;
			GetVersionStrings(BencherUtils.GetAssembly(t), out fileVersion, out assemblyVersion);
			return string.Format(formatString, assemblyVersion, fileVersion);
		}


		/// <summary>
		/// Verifies the data in toEnumerate. Uses the salesorderid retriever set in the ctor to obtain the SalesOrderId from each element in the enumerable
		/// specified
		/// </summary>
		/// <param name="toEnumerate">To enumerate.</param>
		/// <returns>the total number of elements enumerated, or -1 if the row contains no data (the func returned a value less or equal to 0)</returns>
		private int VerifyData(IEnumerable<T> toEnumerate)
		{
			int amount = 0;
			foreach(var v in toEnumerate)
			{
				if(VerifyElement(v) <= 0)
				{
					return -1;
				}
				amount++;
			}
			return amount;
		}


		private void VerifyGraph(IEnumerable<T> toEnumerate, BenchResult resultContainer)
		{
			int amount = 0;
			// parents
			foreach(var v in toEnumerate)
			{
				if(!VerifyGraphElement(v, resultContainer))
				{
					// parent not loaded, fail complete graph
					return;
				}
				amount++;
			}
			resultContainer.IncNumberOfRowsForType(typeof(T), amount);
		}


		/// <summary>
		/// Verifies the element specified, by calling the set SalesOrderId func and passing the specified element to it. The func should return
		/// a value higher than 0
		/// </summary>
		/// <param name="toVerify">To verify.</param>
		/// <returns>the value returned from the func, or -1 if failed</returns>
		private int VerifyElement(T toVerify)
		{
			if(toVerify == null)
			{
				return -1;
			}
			return _salesOrderIdRetriever(toVerify);
		}


		private bool VerifyGraphElement(T parent, BenchResult resultContainer)
		{
			var toReturn = VerifyElement(parent);
			if(toReturn <= 0)
			{
				return false;
			}
			VerifyGraphElementChildren(parent, resultContainer);
			return resultContainer.NumberOfRowsFetchedPerType.Count == 2 && resultContainer.NumberOfRowsFetchedPerType.All(kvp=>kvp.Value > 0);
		}


		/// <summary>
		/// Gets the version strings to be used in the framework version name
		/// </summary>
		/// <param name="toProbe">assembly to use for version/ fileversion retrieval</param>
		/// <param name="fileVersion">The file version, as defined in the specified assembly.</param>
		/// <param name="assemblyVersion">The assembly version of the specified assembly, in 4 digits.</param>
		private void GetVersionStrings(Assembly toProbe, out string fileVersion, out string assemblyVersion)
		{
			fileVersion = string.Empty;
			assemblyVersion = string.Empty;
			if(toProbe != null)
			{
				assemblyVersion = toProbe.GetName().Version.ToString();
				fileVersion = FileVersionInfo.GetVersionInfo(toProbe.Location).FileVersion;
			}
		}


		/// <summary>
		/// Calculates the standard deviation of the values specified using the mean specified.
		/// </summary>
		/// <param name="values">The values.</param>
		/// <param name="mean">The mean.</param>
		/// <returns></returns>
		private double CalculateSD(List<double> values, double mean)
		{
			if(!values.Any())
			{
				return 0.0;
			}
			// see: http://www.mathsisfun.com/data/standard-deviation.html
			var variance = values.Select(v => v - mean).Select(v => v * v).Average();
			return Math.Sqrt(variance);
		}


		#region Properties
		/// <summary>
		/// Gets the individual fetch mean, calculated by <see cref="CalculateStatistics"/>
		/// </summary>
		public double IndividualFetchMean {get ; private set;}
		/// <summary>
		/// Gets the set fetch mean, calculated by <see cref="CalculateStatistics"/>
		/// </summary>
		public double SetFetchMean {get; private set;}
		/// <summary>
		/// Gets the enumeration mean, calculated by <see cref="CalculateStatistics"/>
		/// </summary>
		public double EnumerationMean {get; private set;}
		/// <summary>
		/// Gets the individual fetch standard deviation, calculated by <see cref="CalculateStatistics"/>
		/// </summary>
		public double IndividualFetchSD { get; private set; }
		/// <summary>
		/// Gets the set fetch standard deviation, calculated by <see cref="CalculateStatistics"/>
		/// </summary>
		public double SetFetchSD { get; private set; }
		/// <summary>
		/// Gets the enumeration standard deviation, calculated by <see cref="CalculateStatistics"/>
		/// </summary>
		public double EnumerationSD { get; private set; }
		/// <summary>
		/// Gets the eager load fetch mean, calculated by <see cref="CalculateStatistics"/>
		/// </summary>
		public double EagerLoadFetchMean { get; private set; }
		/// <summary>
		/// Gets the eager load fetch standard deviation, calculated by <see cref="CalculateStatistics"/>
		/// </summary>
		public double EagerLoadFetchSD { get; private set; }
		/// <summary>
		/// Gets the async eager load fetch mean, calculated by <see cref="CalculateStatistics"/>
		/// </summary>
		public double AsyncEagerLoadFetchMean { get; private set; }
		/// <summary>
		/// Gets the async eager load fetch standard deviation, calculated by <see cref="CalculateStatistics"/>
		/// </summary>
		public double AsyncEagerLoadFetchSD { get; private set; }
		/// <summary>
		/// Gets a value indicating whether the fetch uses some form of caching (resultset caching, element caching)
		/// </summary>
		public bool UsesCaching { get; private set; }
		/// <summary>
		/// Gets a value indicating whether the fetch results in change tracked elements or not. 
		/// </summary>
		public bool UsesChangeTracking { get; private set; }
		/// <summary>
		/// Gets a value indicating whether this bencher supports eager loading. If true, this bencher will participate in eager loading benchmarks
		/// </summary>
		public bool SupportsEagerLoading { get; private set; }
		/// <summary>
		/// Gets a value indicating whether this bencher supports asynchronous tasks. If true, this bencher will participate in asynchronous benchmarks.
		/// </summary>
		public bool SupportsAsync { get; private set; }
		#endregion
	}
}
