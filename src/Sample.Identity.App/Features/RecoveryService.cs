﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sample.Identity.App.Contracts;
using Sample.Identity.App.Transfers.Recovery;
using Sample.Identity.Domain.Contracts;
using Sample.Identity.Domain.Entities;
using Sample.Identity.Domain.Enumerators;
using Sample.Identity.Domain.ValueObjects;
using Sample.Identity.Infra.Contracts;
using Sample.Identity.Infra.Models;

namespace Sample.Identity.App.Features
{
    public class RecoveryService : IRecoveryService
    {
        private readonly INotificationService notification;
        private readonly IUnitOfWork unitOfWork;
        private readonly IUserDomainService domainService;
        private readonly Serilog.ILogger logger;
        private readonly ICacheManager cacheManager;
        private readonly AppSettings settings;

        public RecoveryService(INotificationService notification, IOptions<AppSettings> settings, ICacheManager cacheManager, IUnitOfWork unitOfWork, IUserDomainService domainService, Serilog.ILogger logger)
        {
            this.unitOfWork = unitOfWork;
            this.domainService = domainService;
            this.logger = logger;
            this.cacheManager = cacheManager;
            this.settings = settings.Value;
            this.notification = notification;
        }

        public void SendRecoveryCode(PasswordRecoveryRequestTransfer model)
        {
            // Find the user
            User user = unitOfWork.UserRepository.Get(e => e.UserName == model.UserName).FirstOrDefault();

            if (user != null)
            {
                SendRecoveryCode(user, model.NotificationType);

                return;
            }

            logger.Information($"User not found on {nameof(SendRecoveryCode)}. | User: {model.UserName}.");
        }

        private void SendRecoveryCode(User user, NotificationType type)
        {
            // Create a new record of recovery code to send
            RecoveryCode recovery = new RecoveryCode(settings.PasswordRecoveryTimespan);

            // Persist in cache to retrieve as a verification way
            cacheManager.Add($"password-recovery-{user.UserName}", recovery, recovery.ExpireTime);

            // Send a notification
            Notify(user, recovery.Code, type);
        }

        /// <summary>
        /// Verify the confirmation code sent as notification by SendRecoveryCode method
        /// </summary>
        /// <param name="model"></param>
        /// <returns>green light id to update password</returns>
        public RecoveryCode ConfirmRecoveryCode(PasswordRecoveryConfirmTransfer model)
        {
            // Find the user
            User user = unitOfWork.UserRepository.Get(e => e.UserName == model.UserName).FirstOrDefault();

            if (user != null)
            {
                return ConfirmRecoveryCode(user, model.ConfirmationCode);
            }

            logger.Information($"User not found on {nameof(ConfirmRecoveryCode)}. | User: {model.UserName}.");

            return default;
        }

        private RecoveryCode ConfirmRecoveryCode(User user, string code)
        {
            RecoveryCode recovery = cacheManager.Get<RecoveryCode>($"password-recovery-{user.UserName}");

            if (recovery is null || !recovery.Equals(code))
            {
                logger.Information($"Divergent code.| Expected: {recovery?.Code} | Current: {code} | User: {user.UserName}.");

                return null;
            }

            user.ForgotPassword(recovery);

            // Store it as a permission to update password
            unitOfWork.UserRepository.Update(user);

            // Commit
            unitOfWork.Save();

            //Remove from cache to not use it anymore
            cacheManager.Remove($"password-recovery-{user.UserName}");

            // Return the verification
            return recovery;
        }

        /// <summary>
        /// Change user password using the confirmation generated by ConfirmRecoveryCode method
        /// </summary>
        /// <param name="model"></param>
        /// <returns>bool</returns>
        public bool ChangePassword(PasswordRecoveryTransfer model)
        {
            // Find the user
            User user = unitOfWork.UserRepository.Get(e => e.UserName == model.UserName).FirstOrDefault();

            if (user != null)
            {
                return ChangePassword(user, model.RecoveryId, model.Password);
            }

            logger.Information($"User not found on {nameof(ChangePassword)}. | User: {model.UserName}.");

            return default;
        }

        private bool ChangePassword(User user, string recoveryId, string password)
        {
            bool result = domainService.UpdateUserRecoveredPassword(user, recoveryId, password);

            if (result)
            {
                // Store it as a permission to update password
                unitOfWork.UserRepository.Update(user);

                // Commit
                unitOfWork.Save();
            }

            return result;
        }

        private void Notify(User user, string code, NotificationType type)
        {
            switch (type)
            {
                case NotificationType.SMS:

                    Task.Factory.StartNew(() => notification.SendRecoverySms(code, user.PhoneNumber));

                    break;

                case NotificationType.EMAIL:

                    Task.Factory.StartNew(() => notification.SendRecoveryEmail(code, user.Email, user.FirstName));

                    break;
            }
        }
    }
}