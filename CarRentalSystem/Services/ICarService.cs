using CarRentalSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CarRentalSystem.Services
{
    public interface ICarService
    {
        Task<IEnumerable<Car>> GetAllCarsAsync();
        Task<IEnumerable<Car>> GetAvailableCarsAsync();
        Task<Car> GetCarByIdAsync(int id);
        Task AddCarAsync(Car car);
        Task UpdateCarAsync(Car car);
        Task DeleteCarAsync(int id);
        Task AddReviewAsync(Review review);
        Task AddCarImageAsync(CarImage image);
    }
}
