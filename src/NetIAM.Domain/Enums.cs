namespace NetIAM.Domain.Enums;

public enum ExternalProviderType
{
    DingTalk = 1,
    Feishu = 2,
    WeCom = 3
}

public enum IdentitySourceProviderType
{
    DingTalk = 1,
    Feishu = 2,
    WeCom = 3
}

public enum SubjectType
{
    User = 1,
    Group = 2,
    Organization = 3
}

public enum SyncStatus
{
    Success = 1,
    Partial = 2,
    Failed = 3
}

public enum DataOriginType
{
    Local = 1,
    DingTalk = 2,
    Feishu = 3,
    WeCom = 4
}

public enum PermissionGrantEffect
{
    Allow = 1,
    Deny = 2
}

public enum SamlBindingType
{
    HttpPost = 1,
    HttpRedirect = 2
}
