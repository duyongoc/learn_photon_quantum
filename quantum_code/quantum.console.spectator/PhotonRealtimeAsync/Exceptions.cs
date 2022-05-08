using Photon.Realtime;
using System;

namespace PhotonRealtimeAsync {
  public class DisconnectException : Exception {
    public DisconnectCause Cause;
    public DisconnectException(DisconnectCause cause) : base(cause.ToString()) {
      Cause = cause;
    }
  }

  public class AuthenticationFailedException : Exception {
    public AuthenticationFailedException(string message) : base(message) {
    }
  }

  public class OperationException : Exception {
    public short ErrorCode;
    public OperationException(short errorCode, string message) : base($"{message} (ErrorCode: {errorCode})") {
      ErrorCode = errorCode;
    }
  }

  public class OperationStartException : Exception {
    public OperationStartException(string message) : base(message) {
    }
  }

  public class OperationTimeoutException : Exception {
    public OperationTimeoutException(string message) : base(message) {
    }
  }
}
