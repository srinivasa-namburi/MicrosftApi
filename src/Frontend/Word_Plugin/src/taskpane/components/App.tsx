// import { DefaultButton } from "@fluentui/react";
import * as React from "react";
import Progress from "./Progress";

import { AuthenticatedTemplate, UnauthenticatedTemplate, useIsAuthenticated, useMsal } from "@azure/msal-react";
import { FluentProvider, Subtitle1, makeStyles, shorthands, tokens } from "@fluentui/react-components";

import { useEffect } from "react";
import { Constants } from "../Constants";
// import { UserSettingsMenu } from "./components/header/UserSettingsMenu";
// import { PluginGallery } from "./components/open-api-plugins/PluginGallery";
import { AuthHelper } from "../libs/auth/AuthHelper";
import { useChat, useFile } from "../libs/hooks";
import { AlertType } from "../libs/models/AlertType";
import { useAppDispatch, useAppSelector } from "../redux/app/hooks";
import { RootState } from "../redux/app/store";
import { FeatureKeys } from "../redux/features/app/AppState";
import { addAlert, setActiveUserInfo, setServiceOptions } from "../redux/features/app/appSlice";
import { semanticKernelDarkTheme, semanticKernelLightTheme } from "../styles";
import { BackendProbe, ChatView, Error, Loading, Login } from "./views";

/* global Word, require */

export const useClasses = makeStyles({
    container: {
        display: "flex",
        flexDirection: "column",
        height: "100vh",
        width: "100%",
        ...shorthands.overflow("hidden"),
    },
    header: {
        alignItems: "center",
        backgroundColor: tokens.colorBrandForeground2,
        color: tokens.colorNeutralForegroundOnBrand,
        display: "flex",
        "& h1": {
            paddingLeft: tokens.spacingHorizontalXL,
            display: "flex",
        },
        height: "48px",
        justifyContent: "space-between",
        width: "100%",
    },
    persona: {
        marginRight: tokens.spacingHorizontalXXL,
    },
    cornerItems: {
        display: "flex",
        ...shorthands.gap(tokens.spacingHorizontalS),
    },
});

enum AppState {
    ProbeForBackend,
    SettingUserInfo,
    ErrorLoadingUserInfo,
    LoadingChats,
    Chat,
    SigningOut,
}

export interface AppProps {
    title: string;
    isOfficeInitialized: boolean;
}

export const App: React.FC<AppProps> = ({ title, isOfficeInitialized }) => {
    const classes = useClasses();

    const [appState, setAppState] = React.useState(AppState.ProbeForBackend);
    const dispatch = useAppDispatch();

    const { instance, inProgress } = useMsal();
    const { activeUserInfo, features } = useAppSelector((state: RootState) => state.app);
    const isAuthenticated = useIsAuthenticated();

    const chat = useChat();
    const file = useFile();

    useEffect(() => {
        if (isAuthenticated) {
            if (appState === AppState.SettingUserInfo) {
                if (activeUserInfo === undefined) {
                    const account = instance.getActiveAccount();
                    if (!account) {
                        setAppState(AppState.ErrorLoadingUserInfo);
                    } else {
                        dispatch(
                            setActiveUserInfo({
                                id: account.homeAccountId,
                                email: account.username, // username in an AccountInfo object is the email address
                                username: account.name ?? account.username,
                            })
                        );

                        // Privacy disclaimer for internal Microsoft users
                        if (account.username.split("@")[1] === "microsoft.com") {
                            dispatch(
                                addAlert({
                                    message:
                                        "By using Chat Copilot, you agree to protect sensitive data, not store it in chat, and allow chat history collection for service improvements. This tool is for internal use only.",
                                    type: AlertType.Info,
                                })
                            );
                        }

                        setAppState(AppState.LoadingChats);
                    }
                } else {
                    setAppState(AppState.LoadingChats);
                }
            }
        }

        if ((isAuthenticated || !AuthHelper.IsAuthAAD) && appState === AppState.LoadingChats) {
            void Promise.all([
                // Load all chats from memory
                chat.loadChats().then((succeeded) => {
                    if (succeeded) {
                        setAppState(AppState.Chat);
                    }
                }),

                // Check if content safety is enabled
                file.getContentSafetyStatus(),

                // Load service options
                chat.getServiceOptions().then((serviceOptions) => {
                    if (serviceOptions) {
                        dispatch(setServiceOptions(serviceOptions));
                    }
                }),
            ]);
        }

        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [instance, inProgress, isAuthenticated, appState]);

    const click = async () => {
        return Word.run(async (context) => {
            /**
             * Insert your Word code here
             */

            // insert a paragraph at the end of the document.
            const paragraph = context.document.body.insertParagraph("Hello", Word.InsertLocation.end);

            // change the paragraph color to blue.
            paragraph.font.color = "blue";

            await context.sync();
        });
    };

    if (!isOfficeInitialized) {
        return (
            <Progress
                title={title}
                logo={require("./../../../assets/logo-filled.png")}
                message="Please sideload your addin to see app body."
            />
        );
    }

    return (
        <FluentProvider
            className="app-container"
            theme={features[FeatureKeys.DarkMode].enabled ? semanticKernelDarkTheme : semanticKernelLightTheme}
        >
            {AuthHelper.IsAuthAAD ? (
                <>
                    <UnauthenticatedTemplate>
                        <div className={classes.container}>
                            <div className={classes.header}>
                                <Subtitle1 as="h1">{Constants.ui.header}</Subtitle1>
                            </div>
                            {appState === AppState.SigningOut && <Loading text="Signing you out..." />}
                            {appState !== AppState.SigningOut && <Login />}
                        </div>
                    </UnauthenticatedTemplate>
                    <AuthenticatedTemplate>
                        <Chat classes={classes} appState={appState} setAppState={setAppState} />
                    </AuthenticatedTemplate>
                </>
            ) : (
                <Chat classes={classes} appState={appState} setAppState={setAppState} />
            )}
        </FluentProvider>
    );

    // return (
    //     <div className="ms-welcome">
    //         <p className="ms-font-l">
    //             Modify the source files, then click <b>Run</b>.
    //         </p>
    //         <DefaultButton className="ms-welcome__action" iconProps={{ iconName: "ChevronRight" }} onClick={click}>
    //             Run
    //         </DefaultButton>
    //         <ChatWindow />
    //     </div>
    // );
};

const Chat = ({
    classes,
    appState,
    setAppState,
}: {
    classes: ReturnType<typeof useClasses>;
    appState: AppState;
    setAppState: (state: AppState) => void;
}) => {
    return (
        <div className={classes.container}>
            {/* <div className={classes.header}>
                <Subtitle1 as="h1">{Constants.ui.header}</Subtitle1>
                {appState > AppState.SettingUserInfo && (
                    <div className={classes.cornerItems}>
                        <div data-testid="logOutMenuList" className={classes.cornerItems}>
                            <PluginGallery />
                            <UserSettingsMenu
                                setLoadingState={() => {
                                    setAppState(AppState.SigningOut);
                                }}
                            />
                        </div>
                    </div>
                )}
            </div> */}
            {appState === AppState.ProbeForBackend && (
                <BackendProbe
                    uri={process.env.REACT_APP_BACKEND_URI as string}
                    onBackendFound={() => {
                        if (AuthHelper.IsAuthAAD) {
                            setAppState(AppState.SettingUserInfo);
                        } else {
                            setAppState(AppState.LoadingChats);
                        }
                    }}
                />
            )}
            {appState === AppState.SettingUserInfo && (
                <Loading text={"Hang tight while we fetch your information..."} />
            )}
            {appState === AppState.ErrorLoadingUserInfo && (
                <Error text={"Oops, something went wrong. Please try signing out and signing back in."} />
            )}
            {appState === AppState.LoadingChats && <Loading text="Loading Chats..." />}
            {appState === AppState.Chat && <ChatView />}
        </div>
    );
};

// import * as React from "react";
// import { DefaultButton } from "@fluentui/react";
// import Header from "./Header";
// import HeroList, { HeroListItem } from "./HeroList";
// import Progress from "./Progress";

// /* global Word, require */

// export interface AppProps {
//     title: string;
//     isOfficeInitialized: boolean;
// }

// export interface AppState {
//     listItems: HeroListItem[];
// }

// export default class App extends React.Component<AppProps, AppState> {
//     constructor(props, context) {
//         super(props, context);
//         this.state = {
//             listItems: [],
//         };
//     }

//     componentDidMount() {
//         this.setState({
//             listItems: [
//                 {
//                     icon: "Ribbon",
//                     primaryText: "Achieve more with Office integration",
//                 },
//                 {
//                     icon: "Unlock",
//                     primaryText: "Unlock features and functionality",
//                 },
//                 {
//                     icon: "Design",
//                     primaryText: "Create and visualize like a pro",
//                 },
//             ],
//         });
//     }

//     click = async () => {
//         return Word.run(async (context) => {
//             /**
//              * Insert your Word code here
//              */

//             // insert a paragraph at the end of the document.
//             const paragraph = context.document.body.insertParagraph("Hello", Word.InsertLocation.end);

//             // change the paragraph color to blue.
//             paragraph.font.color = "blue";

//             await context.sync();
//         });
//     };

//     render() {
//         const { title, isOfficeInitialized } = this.props;

//         if (!isOfficeInitialized) {
//             return (
//                 <Progress
//                     title={title}
//                     logo={require("./../../../assets/logo-filled.png")}
//                     message="Please sideload your addin to see app body."
//                 />
//             );
//         }

//         return (
//             <div className="ms-welcome">
//                 <Header
//                     logo={require("./../../../assets/logo-filled.png")}
//                     title={this.props.title}
//                     message="Welcome"
//                 />
//                 <HeroList message="Discover what Office Add-ins can do for you today!" items={this.state.listItems}>
//                     <p className="ms-font-l">
//                         Modify the source files, then click <b>Run</b>.
//                     </p>
//                     <DefaultButton
//                         className="ms-welcome__action"
//                         iconProps={{ iconName: "ChevronRight" }}
//                         onClick={this.click}
//                     >
//                         Run
//                     </DefaultButton>
//                 </HeroList>
//             </div>
//         );
//     }
// }
