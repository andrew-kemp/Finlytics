import React from 'react';

const FinlyticsLogo = ({ size }) => {
    return (
        <img
            src="/finlytics-logo.png"
            alt="Finlytics IT"
            className="finlytics-logo"
            style={size ? { width: size, height: size, objectFit: 'contain', flexShrink: 0 } : undefined}
        />
    );
};

export default FinlyticsLogo;
