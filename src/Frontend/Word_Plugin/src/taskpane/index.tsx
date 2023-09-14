import { initializeIcons } from "@fluentui/font-icons-mdl2";
import * as ReactDOM from "react-dom";
import { App } from "./components/App";

import { IPublicClientApplication, PublicClientApplication } from "@azure/msal-browser";
import { MsalProvider } from "@azure/msal-react";
// import ReactDOM from "react-dom/client";
import { Provider as ReduxProvider } from "react-redux";
// import WebApp from "./App";
import MissingEnvVariablesError from "./components/views/MissingEnvVariablesError";
// import "./index.css";
import { AuthHelper } from "./libs/auth/AuthHelper";
import { store } from "./redux/app/store";

import React from "react";
import { getMissingEnvVariables } from "./checkEnv";

/* global document, Office, module, require */

initializeIcons();

let isOfficeInitialized = false;

const title = "Contoso Task Pane Add-in";

const render = (Component) => {
    ReactDOM.render(
        <Component />,
        // <Component title={title} isOfficeInitialized={isOfficeInitialized} />,
        document.getElementById("container")
    );
};

/* Render application after Office initializes */
Office.onReady(() => {
    isOfficeInitialized = true;

    const missingEnvVariables = getMissingEnvVariables();
    const validEnvFile = missingEnvVariables.length === 0;
    const shouldUseMsal = validEnvFile && AuthHelper.IsAuthAAD;

    let msalInstance: IPublicClientApplication | null = null;
    if (shouldUseMsal) {
        msalInstance = new PublicClientApplication(AuthHelper.msalConfig);

        void msalInstance.handleRedirectPromise().then((response) => {
            if (response) {
                msalInstance?.setActiveAccount(response.account);
            }
        });
    }

    const AppWithTheme = () => {
        return (
            <React.StrictMode>
                {validEnvFile ? (
                    <ReduxProvider store={store}>
                        {/* eslint-disable @typescript-eslint/no-non-null-assertion */}
                        {shouldUseMsal ? (
                            <MsalProvider instance={msalInstance!}>
                                <App title={title} isOfficeInitialized={isOfficeInitialized} />
                            </MsalProvider>
                        ) : (
                            <App title={title} isOfficeInitialized={isOfficeInitialized} />
                        )}
                        {/* eslint-enable @typescript-eslint/no-non-null-assertion */}
                    </ReduxProvider>
                ) : (
                    <MissingEnvVariablesError missingVariables={missingEnvVariables} />
                )}
            </React.StrictMode>
        );
    };

    render(AppWithTheme);
});

if ((module as any).hot) {
    (module as any).hot.accept("./components/App", () => {
        const NextApp = require("./components/App").default;
        render(NextApp);
    });
}
